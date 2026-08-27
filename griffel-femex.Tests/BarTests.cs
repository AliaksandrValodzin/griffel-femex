using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// Schema 1.10: the member half of what one real SAF workbook showed was missing.
    ///
    /// Four more of <c>FEMEX_SAF_Fit.md</c> §4's silently-wrong concepts land here —
    /// member behaviour, the system line, the analysis-versus-structural
    /// eccentricity split, and the varying member. The evidence for them differs
    /// sharply and the tests say which is which:
    /// <c>Behaviour in analysis</c> is populated on every one of the reference
    /// workbook's forty-two members and is <c>Axial force only</c> on thirty-three of
    /// them, where the four analysis-eccentricity columns are <b>zero on every row of
    /// every published SAF file</b>. One closes a defect a real import would hit
    /// immediately; the other ships against no evidence at all, which is a reason to
    /// keep it small rather than to leave it out.
    ///
    /// The taper is deliberately the weakest of the four, and
    /// <see cref="ATaper_IsOneSpan_NotSAFsVaryingMember"/> is where that is written
    /// down.
    /// </summary>
    public class BarTests
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

        // ----- Additivity -----

        [Fact]
        public void AModelUsingNoneOfIt_GainsNotOneByte()
        {
            // Four nullable properties with no initializer, so a 1.9 model
            // re-serialised at 1.10 differs by its version stamp and nothing else.
            // Asserted on the bar's own serialized form: a plate's alignment is a
            // non-nullable SurfaceAlignment that has always been written, and
            // matching the whole file would find that instead.
            string bar = System.Text.Json.JsonSerializer.Serialize(
                SampleModels.Build().Column(), FemexModel.JsonOptions);

            Assert.DoesNotContain("\"behaviour\"", bar);
            Assert.DoesNotContain("\"alignment\"", bar);
            Assert.DoesNotContain("\"eccentricity\"", bar);
            Assert.DoesNotContain("\"endSectionId\"", bar);
        }

        // ----- Behaviour -----

        [Fact]
        public void ABehaviour_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Column().Behaviour = BarBehaviour.AxialOnly;

            Assert.Equal(BarBehaviour.AxialOnly, FemexModel.FromJson(model.ToJson()).Column().Behaviour);
        }

        [Fact]
        public void NullBehaviour_IsNotTheSameFactAsStandard()
        {
            // Silence says the file was written before the concept existed; a stated
            // Standard is an author saying so. The same distinction RestraintSense
            // draws, and what keeps an existing file byte-identical.
            var model = SampleModels.Build();
            Assert.Null(model.Column().Behaviour);

            model.Column().Behaviour = BarBehaviour.Standard;

            Assert.Contains("\"behaviour\": \"Standard\"", model.ToJson());
        }

        [Fact]
        public void ATensionOnlyBarWhoseAxialDofIsAlsoReleased_IsAWarning()
        {
            // Such a member carries axial force and nothing else, so releasing that
            // too leaves it resisting nothing at all. The lookup needed is element
            // id to hinge, because a bar carries no hinge of its own.
            var model = SampleModels.Build();
            model.Column().Behaviour = BarBehaviour.TensionOnly;
            model.Hinges.Single(h => h.Id == 1).Ux = Release.Full();

            AssertWarns(model, "is TensionOnly and hinge 1 releases its ux");
        }

        [Fact]
        public void AStandardBarWhoseAxialDofIsReleased_IsSilent()
        {
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 1).Ux = Release.Full();

            Assert.Empty(model.Validate());
        }

        // ----- Alignment -----

        [Fact]
        public void AnAlignment_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Column().Alignment = BarAlignment.TopLeft;

            Assert.Equal(BarAlignment.TopLeft, FemexModel.FromJson(model.ToJson()).Column().Alignment);
        }

        // ----- Eccentricity -----

        [Fact]
        public void AnEccentricity_RoundTrips_AndKeepsTheTwoFamiliesApart()
        {
            // The split is the point of the type: structural moves the picture,
            // analysis moves the answer, and a receiver that fuses them produces
            // geometry that looks right and stiffness that is wrong.
            var model = SampleModels.Build();
            model.Column().Eccentricity = new BarEccentricity
            {
                StructuralZBegin = 0.25,
                AnalysisYEnd = -0.15,
            };

            var restored = FemexModel.FromJson(model.ToJson()).Column().Eccentricity!;

            Assert.Equal(0.25, restored.StructuralZBegin);
            Assert.Equal(-0.15, restored.AnalysisYEnd);
            Assert.Null(restored.StructuralYBegin);
            Assert.True(restored.MovesTheAnalysisLine());
        }

        [Fact]
        public void APurelyStructuralEccentricity_DoesNotMoveTheAnalysisLine()
        {
            var eccentricity = new BarEccentricity { StructuralYBegin = 0.3, StructuralYEnd = 0.3 };

            Assert.False(eccentricity.IsEmpty());
            Assert.False(eccentricity.MovesTheAnalysisLine());
        }

        [Fact]
        public void AnEmptyEccentricityBlock_IsAWarning()
        {
            var model = SampleModels.Build();
            model.Column().Eccentricity = new BarEccentricity();

            AssertWarns(model, "carries an eccentricity block stating no offset at all");
        }

        // ----- Taper -----

        [Fact]
        public void ATaper_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Sections.Add(new Rectangle(4, "R300x900", 0.3, 0.9));
            model.Column().EndSectionId = 4;

            Assert.Equal(4, FemexModel.FromJson(model.ToJson()).Column().EndSectionId);
            Assert.Empty(model.Validate());
        }

        [Fact]
        public void ATaper_IsOneSpan_NotSAFsVaryingMember()
        {
            // Written down rather than assumed: SAF states a varying member as
            // spans with their own sections and alignments, relative spans summing
            // to 1.0, and the reference workbook's one example has three of them.
            // A single linear taper carries the one-span case exactly and turns the
            // rest into an approximation an adapter has to report — a haunched
            // rafter still arrives with the wrong moment distribution, now with a
            // message attached.
            var model = SampleModels.Build();
            model.Sections.Add(new Rectangle(4, "R300x900", 0.3, 0.9));
            model.Column().EndSectionId = 4;

            // The fallback a receiver that ignores the taper builds from is
            // unchanged, which is the degrade-don't-lose rule sections already
            // follow.
            Assert.Equal(1, model.Column().SectionId);
        }

        [Fact]
        public void ATaperToAnAbsentSection_IsAnError()
        {
            var model = SampleModels.Build();
            model.Column().EndSectionId = 99;

            AssertErrors(model, "Bar 1 references unknown end section 99.");
        }

        [Fact]
        public void ATaperToItself_IsAWarning()
        {
            var model = SampleModels.Build();
            model.Column().EndSectionId = model.Column().SectionId;

            AssertWarns(model, "tapers from section 1 to itself, which says nothing");
        }

        [Fact]
        public void ATaperBetweenTwoShapes_IsAWarning()
        {
            // Section 1 is a rectangle and section 2 a circle; nothing can build a
            // member that varies linearly between them, and a receiver falling back
            // on SectionId gets the prismatic member.
            var model = SampleModels.Build();
            model.Column().EndSectionId = 2;

            AssertWarns(model, "which are different shapes; nothing can build a member that varies");
        }

        // ----- Position along a member, the boundary-condition half -----

        [Fact]
        public void ASupportOnABar_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Point, new List<int>())
            {
                BarId = SampleModels.BarId,
                Position = 0.4,
                Uz = Restraint.FixedDof(),
            });

            Assert.Empty(model.Validate(ValidationSeverity.Error));

            var restored = FemexModel.FromJson(model.ToJson()).Supports.Single(s => s.Id == 2);

            Assert.Equal(SampleModels.BarId, restored.BarId);
            Assert.Equal(0.4, restored.Position);
        }

        [Fact]
        public void ALinearSupportAlongABar_RoundTrips()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Linear, new List<int>())
            {
                BarId = SampleModels.BarId,
                Position = 0.2,
                EndPosition = 0.7,
                Uz = Restraint.Spring(500.0),
            });

            var restored = FemexModel.FromJson(model.ToJson()).Supports.Single(s => s.Id == 2);

            Assert.Equal(0.2, restored.Position);
            Assert.Equal(0.7, restored.EndPosition);
        }

        [Fact]
        public void AnAreaSupportFollowingABar_IsAnError()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Area, new List<int>())
            {
                BarId = SampleModels.BarId,
            });

            AssertErrors(model, "follows bar 1 but its target is Area");
        }

        [Fact]
        public void ASupportFollowingBothABarAndAPlate_IsAnError()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Area, new List<int>())
            {
                BarId = SampleModels.BarId,
                PlateId = SampleModels.SlabId,
            });

            AssertErrors(model, "follows both bar 1 and plate 10; use one");
        }

        [Fact]
        public void APointSupportStatingAnEndPosition_IsAnError()
        {
            var model = SampleModels.Build();
            model.Supports.Add(new Support(2, SupportTarget.Point, new List<int>())
            {
                BarId = SampleModels.BarId,
                Position = 0.2,
                EndPosition = 0.7,
            });

            AssertErrors(model, "states an endPosition but its target is Point");
        }

        [Fact]
        public void AHingeAlongABar_RoundTrips_AndNamesNoBarOfItsOwn()
        {
            // ElementId is already the member, so a second reference to it would be
            // two sources of truth about one fact.
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 1).Position = 0.5;

            var restored = FemexModel.FromJson(model.ToJson()).Hinges.Single(h => h.Id == 1);

            Assert.Equal(0.5, restored.Position);
            Assert.Empty(model.Validate(ValidationSeverity.Error));
        }

        [Fact]
        public void AHingePositionOnAPlate_IsAnError()
        {
            var model = SampleModels.Build();
            model.Hinges.Single(h => h.Id == 2).Position = 0.5;

            AssertErrors(model, "states a position but element 10 is a plate");
        }

        // ----- Reading a file from further behind, and from further ahead -----

        [Fact]
        public void ANineFile_IsToldWhatItLacks()
        {
            var model = FemexModel.FromJson("""{ "schemaVersion": "1.9" }""");

            AssertWarns(model, "schemaVersion \"1.9\", written before a member could say how it behaves");
        }

        [Fact]
        public void AnUnknownMemberOnAnEccentricity_SurvivesAndIsNamed()
        {
            var model = FemexModel.FromJson("""
                {
                  "schemaVersion": "1.99",
                  "bars": [
                    {
                      "type": "bar",
                      "id": 1,
                      "startNodeId": 1,
                      "endNodeId": 2,
                      "sectionId": 1,
                      "materialId": 1,
                      "eccentricity": { "analysisYBegin": 0.1, "analysisXBegin": 0.2 }
                    }
                  ]
                }
                """);

            AssertWarns(model, "\"analysisXBegin\", on Bar 1 eccentricity");

            var restored = FemexModel.FromJson(model.ToJson());
            Assert.Equal(0.2, restored.Bars[0].Eccentricity!.UnknownMembers!["analysisXBegin"].GetDouble());
        }
    }
}
