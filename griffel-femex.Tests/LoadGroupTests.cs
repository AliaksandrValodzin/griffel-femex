using griffel_femex.Geometry;
using griffel_femex.Loads;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Schema 1.9: what one real SAF workbook showed the load side was missing.
    ///
    /// Three of <c>FEMEX_SAF_Fit.md</c> §4's eight <i>silently wrong</i> concepts are
    /// closed here — the mandatory load-group reference, the panel's spanning
    /// direction, and a thermal gradient that had no sign convention — together with
    /// the position along a member that <c>FEMEX_SAF_Corpus_Notes.md</c> §6 found
    /// exercised five different ways in the first file it read, and the provenance
    /// pointer §7 decided on.
    ///
    /// Each fact below names the failure it removes. The ones about the
    /// nature/group-type disagreement matter most: that pair is a second source of
    /// truth this bump <i>introduced</i>, and it was designed against rather than
    /// discovered afterwards.
    /// </summary>
    public class LoadGroupTests
    {
        private static void AssertWarns(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Warning && m.Text.Contains(fragment)),
                $"Expected a warning containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        private static void AssertErrors(FemexModel model, string fragment)
        {
            var messages = model.Validate().ToList();
            Assert.True(
                messages.Any(m => m.Severity == ValidationSeverity.Error && m.Text.Contains(fragment)),
                $"Expected an error containing \"{fragment}\". Got: {string.Join(" | ", messages)}");
        }

        /// <summary>A sample model with a group of the given kind holding the dead case.</summary>
        private static FemexModel WithGroup(LoadGroupType type = LoadGroupType.Permanent,
                                            LoadGroupRelation relation = LoadGroupRelation.Standard)
        {
            var model = SampleModels.Build();
            model.LoadGroups.Add(new LoadGroup(1, "LG1", type, relation));
            model.LoadCases.Single(c => c.Number == 1).LoadGroupId = 1;
            return model;
        }

        // ----- Load groups -----

        [Fact]
        public void ALoadGroup_RoundTrips()
        {
            var model = WithGroup(LoadGroupType.Variable, LoadGroupRelation.Exclusive);

            var group = Assert.Single(FemexModel.FromJson(model.ToJson()).LoadGroups);

            Assert.Equal(1, group.Id);
            Assert.Equal("LG1", group.Name);
            Assert.Equal(LoadGroupType.Variable, group.Type);
            Assert.Equal(LoadGroupRelation.Exclusive, group.Relation);
        }

        [Fact]
        public void AModelWithNoGroups_WritesTheEmptyList_AndNothingElse()
        {
            // A root list, like grids and defaultGridIds before it: FEMEX writes
            // "loadGroups": [] rather than omitting the key, so the one new byte
            // every 1.8 file gains is that line and no load case gains anything.
            string json = SampleModels.Build().ToJson();

            Assert.Contains("\"loadGroups\": []", json);
            Assert.DoesNotContain("\"loadGroupId\"", json);
        }

        [Fact]
        public void ACaseNamingAnAbsentGroup_IsAnError()
        {
            var model = SampleModels.Build();
            model.LoadCases.Single(c => c.Number == 1).LoadGroupId = 99;

            AssertErrors(model, "Load case 1 references unknown load group 99.");
        }

        [Fact]
        public void AGroupWithNoCases_IsAWarning()
        {
            var model = SampleModels.Build();
            model.LoadGroups.Add(new LoadGroup(7, "Empty", LoadGroupType.Variable));

            AssertWarns(model, "Load group 7 names no load case");
        }

        [Fact]
        public void AGroupWithNoName_IsAWarning()
        {
            // SAF keys groups by name and treats a duplicate in the sheet as fatal,
            // so this is the same half-step ReportNameKeys already takes for
            // sections, materials and load cases.
            var model = WithGroup();
            model.LoadGroups[0].Name = null;

            AssertWarns(model, "Load group 1 has no name");
        }

        [Fact]
        public void DuplicateGroupIds_AreAnError()
        {
            var model = WithGroup();
            model.LoadGroups.Add(new LoadGroup(1, "LG1 again", LoadGroupType.Variable));

            AssertErrors(model, "Duplicate load group id 1.");
        }

        // ----- The two sources of truth this bump introduced -----

        [Fact]
        public void ADeadCaseInAVariableGroup_IsWarnedAbout()
        {
            // The manufactured silent wrong answer, caught. Nothing forbids the
            // combination, and the partial factors a code applies are exactly what
            // it changes.
            var model = WithGroup(LoadGroupType.Variable);

            AssertWarns(model, "Load case 1 has nature Dead and is in load group 1, typed Variable; " +
                               "Permanent is the type that nature corresponds to");
        }

        [Theory]
        [InlineData(LoadNature.Dead, LoadGroupType.Permanent)]
        [InlineData(LoadNature.Live, LoadGroupType.Variable)]
        [InlineData(LoadNature.Wind, LoadGroupType.Variable)]
        [InlineData(LoadNature.Snow, LoadGroupType.Variable)]
        [InlineData(LoadNature.Temperature, LoadGroupType.Variable)]
        [InlineData(LoadNature.Accidental, LoadGroupType.Accidental)]
        [InlineData(LoadNature.Seismic, LoadGroupType.Seismic)]
        public void TheCompatibilityMap_IsSilentWhereItAgrees(LoadNature nature, LoadGroupType type)
        {
            var model = WithGroup(type);
            model.LoadCases.Single(c => c.Number == 1).Nature = nature;

            Assert.DoesNotContain(model.Validate(), m => m.Text.Contains("is the type that nature"));
        }

        [Fact]
        public void ATensioningGroup_IsWarnedAboutInItsOwnWords()
        {
            // Not an author's slip: FEMEX has no LoadNature for prestress, so every
            // case in such a group disagrees with it and changing the nature is not
            // the fix. It gets its own wording for that reason.
            var model = WithGroup(LoadGroupType.Tensioning);

            AssertWarns(model, "typed Tensioning, which no load nature corresponds to");
        }

        // ----- Panel spanning -----

        [Fact]
        public void APanelDistribution_RoundTrips_AndAModelWithoutOne_OmitsTheKeyEntirely()
        {
            var model = SampleModels.Build();
            Assert.DoesNotContain("\"distribution\"", model.ToJson());

            model.Slab().Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayX, 45.0)
            {
                BarIds = new List<int> { SampleModels.BarId },
            };

            var restored = FemexModel.FromJson(model.ToJson()).Slab().Distribution!;

            Assert.Equal(SurfaceLoadSpanning.OneWayX, restored.Spanning);
            Assert.Equal(45.0, restored.RotationAngle);
            Assert.Equal(new[] { SampleModels.BarId }, restored.BarIds!);
        }

        [Fact]
        public void ARegionDistribution_RoundTrips()
        {
            // A region states its own or inherits the plate's, the rule it already
            // follows for surface property, alignment and offset.
            var model = SampleModels.Build();
            model.Slab().Regions[0].Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayY);

            var restored = FemexModel.FromJson(model.ToJson()).Slab().Regions[0].Distribution!;

            Assert.Equal(SurfaceLoadSpanning.OneWayY, restored.Spanning);
        }

        [Fact]
        public void ADistributionNamingAnAbsentMember_IsAnError()
        {
            var model = SampleModels.Build();
            model.Slab().Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayX)
            {
                BarIds = new List<int> { 4242 },
            };

            AssertErrors(model, "Plate 10 load distribution references unknown bar 4242.");
        }

        [Fact]
        public void ADistributionNamingAPlate_IsAnError()
        {
            // Bars, plates and mesh faces share one id space, so an id that resolves
            // is not yet an id that can receive a distributed panel load.
            var model = SampleModels.Build();
            model.Slab().Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayX)
            {
                BarIds = new List<int> { SampleModels.WallId },
            };

            AssertErrors(model,
                         $"names element {SampleModels.WallId} as its bar, but that element is not a bar");
        }

        [Fact]
        public void ARotatedTwoWayPanel_IsAWarning()
        {
            var model = SampleModels.Build();
            model.Slab().Distribution = new LoadDistribution(SurfaceLoadSpanning.TwoWay, 30.0);

            AssertWarns(model, "nothing reads the angle of a two-way panel");
        }

        [Fact]
        public void AnEmptyMemberList_IsAWarning()
        {
            // "These members, and there are none" is a different claim from null,
            // which means whatever bounds the panel, and it is one no receiver can
            // carry out.
            var model = SampleModels.Build();
            model.Slab().Distribution = new LoadDistribution(SurfaceLoadSpanning.OneWayX)
            {
                BarIds = new List<int>(),
            };

            AssertWarns(model, "barIds list is empty");
        }

        // ----- Position along a member -----

        [Fact]
        public void APointLoadOnABar_RoundTrips_AndNeedsNoNode()
        {
            var model = SampleModels.Build();
            model.Loads.Add(new PointLoad
            {
                Id = 90,
                Label = "P-mid",
                LoadCaseNumber = 1,
                BarId = SampleModels.BarId,
                Position = 0.5,
                Fz = -8.0,
            });

            Assert.Empty(model.Validate(ValidationSeverity.Error));

            var restored = (PointLoad)FemexModel.FromJson(model.ToJson()).Loads.Single(l => l.Id == 90);

            Assert.Equal(SampleModels.BarId, restored.BarId);
            Assert.Equal(0.5, restored.Position);
        }

        [Fact]
        public void ALineLoadExtent_RoundTrips()
        {
            var model = SampleModels.Build();
            var line = model.LinearLoad("L2");
            line.StartPosition = 0.2;
            line.EndPosition = 0.8;

            var restored = FemexModel.FromJson(model.ToJson()).LinearLoad("L2");

            Assert.Equal(0.2, restored.StartPosition);
            Assert.Equal(0.8, restored.EndPosition);
        }

        [Fact]
        public void APositionOutsideTheMember_IsAnError()
        {
            var model = SampleModels.Build();
            model.Loads.Add(new PointLoad
            {
                Id = 90,
                Label = "P-off",
                LoadCaseNumber = 1,
                BarId = SampleModels.BarId,
                Position = 1.4,
            });

            AssertErrors(model, "states a position of 1.4; a position along a member is relative");
        }

        [Fact]
        public void APositionWithNoHost_IsAnError()
        {
            var model = SampleModels.Build();
            model.Loads.Add(new PointLoad
            {
                Id = 90,
                Label = "P-hostless",
                LoadCaseNumber = 1,
                NodeNumber = 2,
                Position = 0.5,
            });

            AssertErrors(model, "states a position but names no bar");
        }

        [Fact]
        public void ABackwardsExtent_IsAWarning()
        {
            var model = SampleModels.Build();
            var line = model.LinearLoad("L2");
            line.StartPosition = 0.8;
            line.EndPosition = 0.2;

            AssertWarns(model, "this extent runs backwards or is empty");
        }

        // ----- Provenance -----

        [Fact]
        public void AParentUid_RoundTrips_AndAModelWithoutOne_OmitsTheKeyEntirely()
        {
            var model = SampleModels.Build();
            Assert.DoesNotContain("\"parentUid\"", model.ToJson());

            model.AssignMissingUids();
            Guid arc = Guid.NewGuid();
            model.Column().ParentUid = arc;

            Assert.Equal(arc, FemexModel.FromJson(model.ToJson()).Column().ParentUid);
        }

        [Fact]
        public void AParentUidNamingNothingInTheModel_IsOnlyAWarning()
        {
            // The point of the field: a chorded arc's parent is an arc that was never
            // a FEMEX object, so an unresolved parent is provenance working rather
            // than a broken reference.
            var model = SampleModels.Build();
            model.AssignMissingUids();
            model.Column().ParentUid = Guid.NewGuid();

            Assert.Empty(model.Validate(ValidationSeverity.Error));
            AssertWarns(model, "which is no object in this model");
        }

        [Fact]
        public void AResolvedParentUid_IsSilent()
        {
            var model = SampleModels.Build();
            model.AssignMissingUids();
            model.Column().ParentUid = model.Slab().Uid;

            Assert.Empty(model.Validate());
        }

        [Fact]
        public void AnObjectParentedToItself_IsAnError()
        {
            var model = SampleModels.Build();
            model.AssignMissingUids();
            model.Column().ParentUid = model.Column().Uid;

            AssertErrors(model, "names itself as its own parentUid");
        }

        [Fact]
        public void TheNilParentUid_IsAnError()
        {
            var model = SampleModels.Build();
            model.Column().ParentUid = Guid.Empty;

            AssertErrors(model, "carries the nil uid");
        }

        // ----- Reading a file from further behind, and from further ahead -----

        [Fact]
        public void AnEightFile_IsToldWhatItLacks()
        {
            var model = FemexModel.FromJson("""{ "schemaVersion": "1.8" }""");

            AssertWarns(model, "schemaVersion \"1.8\", written before load groups existed");
        }

        [Fact]
        public void AnUnknownMemberOnEachNewType_SurvivesAndIsNamed()
        {
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.99",
                  "loadGroups": [ { "id": 1, "name": "LG1", "somethingFrom200": 42 } ],
                  "plates": [
                    {
                      "type": "plate",
                      "id": 1,
                      "nodeIds": [ 1, 2, 3 ],
                      "distribution": { "spanning": "OneWayX", "somethingElse": "yes" }
                    }
                  ]
                }
                """);

            AssertWarns(model, "\"somethingFrom200\", on Load group 1");
            AssertWarns(model, "\"somethingElse\", on Plate 1 distribution");

            var restored = FemexModel.FromJson(model.ToJson());
            Assert.Equal(42, restored.LoadGroups[0].UnknownMembers!["somethingFrom200"].GetInt32());
            Assert.Equal("yes", restored.Plates[0].Distribution!.UnknownMembers!["somethingElse"].GetString());
        }
    }
}
