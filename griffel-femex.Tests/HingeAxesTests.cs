using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Mesh;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The frame a hinge's six releases are measured in: the bar's own local axes on a
    /// member, the edge's on a plate or mesh-face edge.
    ///
    /// <b>These are the tests the convention did not have.</b> Before this, the only
    /// executable statement that a release is local at all was the tension-only bar
    /// rule in <c>ValidateBarCompleteness</c> — which reads <c>Ux</c> as the axial DOF
    /// and is right to, but is one rule inferring a convention rather than stating one.
    /// Everything here is <see cref="FemexModel.TryGetHingeLocalAxes"/> and
    /// <see cref="FemexModel.TryGetEdgeLocalAxes"/> saying it out loud.
    /// </summary>
    public class HingeAxesTests
    {
        /// <summary>Axes are unit vectors, so this is generous by an order of magnitude.</summary>
        private const int Places = 9;

        private static void AssertVector(Vector3d expected, Vector3d actual)
        {
            Assert.Equal(expected.X, actual.X, Places);
            Assert.Equal(expected.Y, actual.Y, Places);
            Assert.Equal(expected.Z, actual.Z, Places);
        }

        private static Hinge HingeById(FemexModel model, int id) => model.Hinges.Single(h => h.Id == id);

        // ----- The edge frame -----

        [Fact]
        public void EdgeLocalAxes_SlabEdge_RunAlongTheEdge_WithThePanelsNormalForZ()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 2, 12,
                                                  out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);   // 2 -> 12, along global +X
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);   // into the panel
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);   // the slab's own normal
        }

        [Fact]
        public void EdgeLocalAxes_AreNotTurnedByThePanelsLocalAxisAngle()
        {
            var model = SampleModels.Build();

            // The sample already sets 15 degrees; 40 is a value nothing else in the
            // fixture depends on, so an answer that moved would move visibly.
            model.Slab().LocalAxisAngle = 40.0;

            Assert.True(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 2, 12,
                                                  out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void EdgeLocalAxes_NamingTheEdgeBackwards_ReversesXAndY()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 12, 2,
                                                  out Vector3d x, out Vector3d y, out Vector3d z));

            // Which is why the order of the two node ids is not cosmetic: a release
            // stated in y is its own opposite in the other reading.
            AssertVector(new Vector3d(-1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, -1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void EdgeLocalAxes_OnAWall_AreNoneOfTheGlobalAxes_InTheSameOrder()
        {
            var model = SampleModels.Build();

            // The wall stands in the y = 0 plane and its contour 1, 42, 12, 2 winds so
            // that the normal faces -Y. The edge 42 -> 12 runs straight up.
            Assert.True(model.TryGetEdgeLocalAxes(SampleModels.WallId, 42, 12,
                                                  out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(0.0, 0.0, 1.0), x);
            AssertVector(new Vector3d(-1.0, 0.0, 0.0), y);   // into the panel
            AssertVector(new Vector3d(0.0, -1.0, 0.0), z);
            AssertVector(z, x.Cross(y));                     // right-handed
        }

        [Fact]
        public void EdgeLocalAxes_AreNotFound_ForAnUnknownPlate_OrAnUnknownNode()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetEdgeLocalAxes(99, 2, 12, out _, out _, out _));
            Assert.False(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 2, 999, out _, out _, out _));
        }

        [Fact]
        public void EdgeLocalAxes_AreNotFound_ForAnEdgeOfNoLength()
        {
            var model = SampleModels.Build();

            // Not an edge validation accepts either — a node is not adjacent to
            // itself — but the lookup answers rather than throws, as every other one
            // in FemexModel.LocalAxes.cs does.
            Assert.False(model.TryGetEdgeLocalAxes(SampleModels.SlabId, 2, 2, out _, out _, out _));
        }

        // ----- Which frame a hinge is in -----

        [Fact]
        public void HingeLocalAxes_OnABar_AreTheBarsOwnAxes_RollIncluded()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetHingeLocalAxes(HingeById(model,1),
                                                   out Vector3d x, out Vector3d y, out Vector3d z));
            Assert.True(model.TryGetBarLocalAxes(SampleModels.BarId,
                                                 out Vector3d bx, out Vector3d by, out Vector3d bz));

            AssertVector(bx, x);
            AssertVector(by, y);
            AssertVector(bz, z);

            // The column carries a 30-degree roll, so this is not the global frame
            // dressed up: rz is a real minor-axis statement about a rotated section.
            Assert.NotEqual(0.0, model.Column().RotationAngle);
        }

        [Fact]
        public void HingeLocalAxes_OnASlabEdge_AreTheEdgeFrame()
        {
            var model = SampleModels.Build();

            Assert.True(model.TryGetHingeLocalAxes(HingeById(model,2),
                                                   out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void HingeLocalAxes_OnAWallEdge_AreTheEdgeFrame_AndTheSampleSaysSo()
        {
            var model = SampleModels.Build();

            // Hinge 3 is the fixture's vertical movement joint: ux released is a slip
            // along the edge, which is straight up here and nowhere near global x.
            Hinge hinge = HingeById(model,3);
            Assert.True(hinge.Ux.Released);

            Assert.True(model.TryGetHingeLocalAxes(hinge, out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(0.0, 0.0, 1.0), x);
            AssertVector(new Vector3d(-1.0, 0.0, 0.0), y);
            AssertVector(new Vector3d(0.0, -1.0, 0.0), z);
        }

        [Fact]
        public void HingeLocalAxes_WithNoNamedEdge_FallBackToTheIndexedContourEdge()
        {
            var model = SampleModels.Build();

            Hinge hinge = HingeById(model,2);
            hinge.EdgeStartNodeId = null;
            hinge.EdgeEndNodeId = null;

            // endOrEdgeIndex 0 is the contour edge 2 -> 12, which is the edge the
            // node pair named: the two addresses agree, which is what makes the
            // fallback a fallback rather than a second convention.
            Assert.True(model.TryGetHingeLocalAxes(hinge, out Vector3d x, out Vector3d y, out Vector3d z));

            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void HingeLocalAxes_OnARegionEdge_TakeThePanelsNormal_NotTheRegionsWinding()
        {
            var model = SampleModels.Build();

            // The stair void wound against its panel, which is legal — a region's
            // contour has no orientation rule of its own — and is exactly the case
            // where "the region's normal" and "the panel's normal" differ.
            PlateRegion region = model.Slab().Regions.Single(r => r.Id == SampleModels.VoidRegionId);
            region.NodeIds.Reverse();

            var hinge = new Hinge(4, HingeTarget.Linear, elementId: SampleModels.SlabId,
                                  endOrEdgeIndex: 0, new List<int> { 34, 33 })
            {
                RegionId = SampleModels.VoidRegionId,
                Rx = Release.Full(),
            };
            model.Hinges.Add(hinge);

            Assert.True(model.TryGetHingeLocalAxes(hinge, out Vector3d x, out Vector3d y, out Vector3d z));

            // 34 -> 33 runs along +X, and z stays the slab's +Z. Had the region's own
            // winding decided, z would be -Z and y would point out of the void
            // instead of into it.
            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void HingeLocalAxes_OnAMeshFace_UseTheFacesOwnNodesAndNormal()
        {
            var model = SampleModels.Build();

            // A generated face has no named edge, so the index is its only address.
            var hinge = new Hinge(4, HingeTarget.Linear, elementId: SampleModels.MeshFaceId,
                                  endOrEdgeIndex: 0, new List<int>())
            {
                Rx = Release.Full(),
            };
            model.Hinges.Add(hinge);

            Assert.True(model.TryGetHingeLocalAxes(hinge, out Vector3d x, out Vector3d y, out Vector3d z));

            // Mesh nodes 1 -> 2, which run along +X on a face wound counter-clockwise
            // seen from above.
            AssertVector(new Vector3d(1.0, 0.0, 0.0), x);
            AssertVector(new Vector3d(0.0, 1.0, 0.0), y);
            AssertVector(new Vector3d(0.0, 0.0, 1.0), z);
        }

        [Fact]
        public void HingeLocalAxes_AreNotFound_ForAnIndexOutsideTheContour()
        {
            var model = SampleModels.Build();

            Hinge hinge = HingeById(model,2);
            hinge.EdgeStartNodeId = null;
            hinge.EdgeEndNodeId = null;
            hinge.EndOrEdgeIndex = 7;

            // The same reading Validate() takes of an index it rejects: no answer,
            // rather than a silent clamp onto some other edge.
            Assert.False(model.TryGetHingeLocalAxes(hinge, out _, out _, out _));
        }

        [Fact]
        public void HingeLocalAxes_AreNotFound_ForAnUnknownElement_OrAnUnknownRegion()
        {
            var model = SampleModels.Build();

            Assert.False(model.TryGetHingeLocalAxes(
                new Hinge(9, HingeTarget.Point, elementId: 999, endOrEdgeIndex: 0, new List<int>()),
                out _, out _, out _));

            Hinge hinge = HingeById(model,2);
            hinge.EdgeStartNodeId = null;
            hinge.EdgeEndNodeId = null;
            hinge.RegionId = 99;

            Assert.False(model.TryGetHingeLocalAxes(hinge, out _, out _, out _));
        }

        [Fact]
        public void EveryHingeInTheSample_ResolvesToARightHandedUnitTriad()
        {
            var model = SampleModels.Build();

            foreach (Hinge hinge in model.Hinges)
            {
                Assert.True(model.TryGetHingeLocalAxes(hinge, out Vector3d x, out Vector3d y, out Vector3d z),
                            $"Hinge {hinge.Id} has no frame.");

                Assert.Equal(1.0, x.Length, Places);
                Assert.Equal(1.0, y.Length, Places);
                Assert.Equal(1.0, z.Length, Places);
                AssertVector(z, x.Cross(y));
            }
        }

        [Fact]
        public void TheSampleModel_StillValidatesClean_WithTheWallJoint()
        {
            var model = SampleModels.Build();

            // The third hinge is a fixture change, and a fixture that starts warning
            // is a fixture every other test has to reason around.
            Assert.DoesNotContain(model.Validate(),
                                  m => m.Severity == ValidationSeverity.Error && m.Text.Contains("Hinge 3"));
        }
    }
}
