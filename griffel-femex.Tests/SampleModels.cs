using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Grids;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using griffel_femex.Materials;
using griffel_femex.Mesh;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The single sample model the test suite is built from: two levels, a column
    /// bar, a slab panel with a drop panel and a void, a wall panel spanning two
    /// levels, one of each load type, three load combinations, a support, a bar
    /// hinge, a plate-edge hinge, and a one-face mesh. Two architectural grids sit
    /// alongside: an unrotated one the whole model defaults to, and a rotated one
    /// the first floor adds by overriding that default.
    ///
    /// There is exactly one node per location, so the model is fully connected and
    /// <see cref="FemexModel.Validate()"/> raises no coincident-node warning. The
    /// column top, the slab's near corner and the wall's top-left corner are all
    /// node 2 — the commented-out numbers record where a second node used to be.
    /// </summary>
    internal static class SampleModels
    {
        // Element ids. Bars, plates and mesh faces share one id space.
        public const int BarId = 1;
        public const int SlabId = 10;
        public const int WallId = 11;
        public const int MeshFaceId = 9001;

        // Region ids, unique within the slab only.
        public const int DropPanelRegionId = 1;
        public const int VoidRegionId = 2;

        // Grid ids, their own id space.
        public const int PrimaryGridId = 1;
        public const int CoreGridId = 2;

        // Load combination numbers, their own id space again. Banded by limit
        // state: 1xx ultimate, 2xx serviceability.
        public const int UltimateCombinationNumber = 101;
        public const int EnvelopeCombinationNumber = 102;
        public const int ExcludedCombinationNumber = 201;

        public const double GroundElevation = 145.50;
        public const double FirstFloorElevation = 148.50;

        public static FemexModel Build()
        {
            var model = new FemexModel
            {
                Units = new Units("m", "kN"),
                Grids =
                {
                    // Set out on the slab: A/1 is the column, B/2 the far corner.
                    new Grid(PrimaryGridId, "Primary")
                    {
                        Extent = new GridExtent(-2.0, 12.0, -2.0, 12.0),
                        Lines =
                        {
                            new OrthogonalGridline("A", GridDirection.Y, 0.0),
                            new OrthogonalGridline("B", GridDirection.Y, 10.0),
                            new OrthogonalGridline("1", GridDirection.X, 0.0),
                            new OrthogonalGridline("2", GridDirection.X, 10.0),
                            new FreeGridline("D1", 0.0, 0.0, 10.0, 5.0),
                        },
                    },

                    // A second, rotated grid, on the first floor only. Its origin
                    // is the drop panel's near corner, so CA/C1 is node 21.
                    new Grid(CoreGridId, "Core", originX: 3.0, originY: 3.0, rotationAngle: 45.0)
                    {
                        Lines =
                        {
                            new OrthogonalGridline("CA", GridDirection.Y, 0.0),
                            new OrthogonalGridline("C1", GridDirection.X, 0.0),
                            new OrthogonalGridline("C2", GridDirection.X, 4.0),
                        },
                    },
                },
                DefaultGridIds = { PrimaryGridId },
                Levels =
                {
                    // GridIds left null: the ground floor inherits the default.
                    new Level(0, "Ground", GroundElevation, 0.00, isGround: true),

                    // An override, which replaces the default rather than adding
                    // to it — hence the primary grid is named again.
                    new Level(1, "First Floor", FirstFloorElevation, 3.00)
                    {
                        GridIds = new List<int> { PrimaryGridId, CoreGridId },
                    },
                },
                Nodes =
                {
                    // Column
                    new Node(1, 0.0, 0.0, levelNumber: 0),
                    new Node(2, 0.0, 0.0, levelNumber: 1),

                    // Slab outer contour, 10 x 10 at first floor
                    // 11 (0, 0, first floor) is the column top: node 2.
                    new Node(12, 10.0, 0.0, levelNumber: 1),
                    new Node(13, 10.0, 10.0, levelNumber: 1),
                    new Node(14, 0.0, 10.0, levelNumber: 1),

                    // Drop panel
                    new Node(21, 3.0, 3.0, levelNumber: 1),
                    new Node(22, 7.0, 3.0, levelNumber: 1),
                    new Node(23, 7.0, 7.0, levelNumber: 1),
                    new Node(24, 3.0, 7.0, levelNumber: 1),

                    // Stair void
                    new Node(31, 8.0, 1.0, levelNumber: 1),
                    new Node(32, 9.5, 1.0, levelNumber: 1),
                    new Node(33, 9.5, 3.0, levelNumber: 1),
                    new Node(34, 8.0, 3.0, levelNumber: 1),

                    // Wall, in the y = 0 plane, spanning ground to first floor.
                    // Three of its four corners are already there: 41 is the column
                    // base (node 1), 43 and 44 are slab corners (nodes 12 and 2).
                    new Node(42, 10.0, 0.0, levelNumber: 0),
                },
                Sections =
                {
                    new Rectangle(1, "R300x500", 0.3, 0.5),
                    new Circle(2, "C400", 0.4),
                    new TSection(3, "T-beam", 0.6, 0.15, 0.2, 0.8),
                },
                SurfaceProperties =
                {
                    new ConstantThickness(1, "SLAB-250", 0.25),
                    new ConstantThickness(2, "DROP-450", 0.45),
                    new ConstantThickness(3, "WALL-300", 0.30),
                },
                Materials =
                {
                    new Material(1, "Concrete C30", 33e9, 0.2, 25.0, 30e6),
                },
                LoadCases =
                {
                    new LoadCase(1, "Dead", LoadNature.Dead),
                    new LoadCase(2, "Thermal", LoadNature.Temperature),
                },
            };

            model.Bars.Add(new Bar(BarId, startNodeId: 1, endNodeId: 2, sectionId: 1, materialId: 1, rotationAngle: 30.0));

            var slab = new Plate(SlabId, new List<int> { 2, 12, 13, 14 }, surfacePropertyId: 1, materialId: 1)
            {
                Name = "L1 slab",
                Alignment = SurfaceAlignment.Top,
                LocalAxisAngle = 15.0,
                Regions =
                {
                    new PlateRegion(DropPanelRegionId, new List<int> { 21, 22, 23, 24 }, PlateRegionKind.Structural, priority: 10)
                    {
                        Name = "Drop panel",
                        SurfacePropertyId = 2,
                        SurfaceOffset = -0.2,
                    },
                    new PlateRegion(VoidRegionId, new List<int> { 31, 32, 33, 34 }, PlateRegionKind.Opening, priority: 20)
                    {
                        Name = "Stair void",
                    },
                },
            };
            model.Plates.Add(slab);

            model.Plates.Add(new Plate(WallId, new List<int> { 1, 42, 12, 2 }, surfacePropertyId: 3, materialId: 1)
            {
                Name = "Ground floor shear wall",
                Behaviour = PlateBehaviour.Membrane,
            });

            model.Loads.Add(new PointLoad { Label = "P1", LoadCaseNumber = 1, NodeNumber = 2, Fz = -10.0, Mx = 2.5 });
            model.Loads.Add(new LinearLoad { Label = "L1", LoadCaseNumber = 1, StartNode = 1, EndNode = 2, MagnitudeStart = -5.0, MagnitudeEnd = -5.0 });
            model.Loads.Add(new AreaLoad { Label = "A1", LoadCaseNumber = 1, PlateId = SlabId, Magnitude = -2.0 });
            model.Loads.Add(new TemperatureLoad { Label = "T1", LoadCaseNumber = 2, ElementIds = { BarId }, DeltaT = 20.0, GradientPerDepth = 5.0 });

            // Both limit states, a non-default CombinationType, and both states of
            // the design-envelope flag. Labels are distinct and non-null so the
            // sample raises no combination warning of its own.
            model.LoadCombinations.Add(new LoadCombination(UltimateCombinationNumber, "U1", LimitState.Ultimate)
            {
                Terms =
                {
                    new LoadCombinationTerm(1, 1.2),
                    new LoadCombinationTerm(2, 1.0),
                },
            });

            model.LoadCombinations.Add(new LoadCombination(EnvelopeCombinationNumber, "U2", LimitState.Ultimate)
            {
                CombinationType = LoadCombinationType.Envelope,
                Terms = { new LoadCombinationTerm(1, 1.35) },
            });

            model.LoadCombinations.Add(new LoadCombination(ExcludedCombinationNumber, "S1", LimitState.Serviceability)
            {
                IncludeInDesignEnvelope = false,
                Terms = { new LoadCombinationTerm(1, 1.0) },
            });

            model.Supports.Add(new Support(1, SupportTarget.Point, new List<int> { 1 })
            {
                Ux = Restraint.FixedDof(),
                Uy = Restraint.FixedDof(),
                Uz = Restraint.FixedDof(),
                Rx = Restraint.Free(),
                Ry = Restraint.Free(),
                Rz = Restraint.Spring(1000.0),
            });

            model.Hinges.Add(new Hinge(1, HingeTarget.Point, elementId: BarId, endOrEdgeIndex: 1, new List<int> { 2 })
            {
                Rz = Release.Full(),
                Ry = Release.Partial(500.0),
            });

            // A hinged slab edge, named by its two contour nodes.
            model.Hinges.Add(new Hinge(2, HingeTarget.Linear, elementId: SlabId, endOrEdgeIndex: 0, new List<int> { 2, 12 })
            {
                EdgeStartNodeId = 2,
                EdgeEndNodeId = 12,
                Rx = Release.Full(),
            });

            model.Mesh = new FemexMesh
            {
                Generator = "test",
                GeneratedAt = "2026-08-05T10:22:00Z",
                Nodes =
                {
                    new MeshNode(1, 0.0, 0.0, FirstFloorElevation, sourceNodeId: 2),
                    new MeshNode(2, 10.0, 0.0, FirstFloorElevation, sourceNodeId: 12),
                    new MeshNode(3, 10.0, 10.0, FirstFloorElevation, sourceNodeId: 13),
                    new MeshNode(4, 0.0, 10.0, FirstFloorElevation, sourceNodeId: 14),
                },
                Faces =
                {
                    new MeshFace(MeshFaceId, new List<int> { 1, 2, 3, 4 }, plateId: SlabId)
                    {
                        SurfacePropertyId = 1,
                        MaterialId = 1,
                        Thickness = 0.25,
                    },
                },
            };

            return model;
        }

        /// <summary>The slab panel of a freshly built sample model.</summary>
        public static Plate Slab(this FemexModel model) => model.Plates.Single(p => p.Id == SlabId);

        /// <summary>The wall panel of a freshly built sample model.</summary>
        public static Plate Wall(this FemexModel model) => model.Plates.Single(p => p.Id == WallId);

        /// <summary>The unrotated grid the slab is set out on.</summary>
        public static Grid PrimaryGrid(this FemexModel model) => model.Grids.Single(g => g.Id == PrimaryGridId);

        /// <summary>The 45-degree grid the first floor also carries.</summary>
        public static Grid CoreGrid(this FemexModel model) => model.Grids.Single(g => g.Id == CoreGridId);
    }
}
