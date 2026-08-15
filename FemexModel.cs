using System.Text.Json;
using System.Text.Json.Serialization;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Grids;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using griffel_femex.Materials;
using griffel_femex.Mesh;

namespace griffel_femex
{
    /// <summary>
    /// The single JSON-serializable root of a FEMEX finite-element model.
    /// Holds the four data blocks (Geometry, Materials, Loads, Boundary
    /// Conditions) as flat lists of entities referenced by integer ids.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// The version of the FEMEX format this model is written in — declared
        /// first, so it is the first key in the file and a reader can branch on it
        /// before parsing anything else.
        ///
        /// Deliberately <b>not</b> initialized: <c>null</c> is how a file written
        /// before load directions existed is told apart from one written after, and
        /// that distinction is not cosmetic. Such a file's distributed loads are
        /// read as acting along global +Z, so its gravity loads carry the wrong
        /// sign. <see cref="Validate()"/> warns about both a null and an
        /// unrecognised version.
        ///
        /// <see cref="ToJson"/> stamps <see cref="CurrentSchemaVersion"/> here when
        /// it is null, so every file FEMEX writes is versioned and every file it
        /// reads keeps the version it had.
        /// </summary>
        public string? SchemaVersion { get; set; }

        /// <summary>
        /// The version this build writes and understands. 1.0 is the unstamped
        /// format that had no load directions; 1.1 added them, together with
        /// <c>LinearLoad.BarId</c> and this field.
        /// </summary>
        public const string CurrentSchemaVersion = "1.1";

        // Optional metadata (length/force convention)
        public Units? Units { get; set; }

        // Geometry
        public List<Grid> Grids { get; set; } = new List<Grid>();

        // The grids every level uses unless it names its own (references Grid.Id).
        public List<int> DefaultGridIds { get; set; } = new List<int>();

        public List<Level> Levels { get; set; } = new List<Level>();
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Section> Sections { get; set; } = new List<Section>();
        public List<SurfaceProperty> SurfaceProperties { get; set; } = new List<SurfaceProperty>();
        public List<Bar> Bars { get; set; } = new List<Bar>();
        public List<Plate> Plates { get; set; } = new List<Plate>();

        // Materials
        public List<Material> Materials { get; set; } = new List<Material>();

        // Loads
        public List<LoadCase> LoadCases { get; set; } = new List<LoadCase>();
        public List<Load> Loads { get; set; } = new List<Load>();
        public List<LoadCombination> LoadCombinations { get; set; } = new List<LoadCombination>();

        // Boundary conditions
        public List<Support> Supports { get; set; } = new List<Support>();
        public List<Hinge> Hinges { get; set; } = new List<Hinge>();

        // The generated finite-element mesh of the plates. Null until the model has
        // been meshed, and omitted from the JSON entirely while it is.
        public FemexMesh? Mesh { get; set; }

        // Shared serializer options for the whole model.
        public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        // ----- Serialization helpers -----

        /// <summary>
        /// Serializes the model, stamping <see cref="SchemaVersion"/> with
        /// <see cref="CurrentSchemaVersion"/> first if it has none. That stamp is
        /// the one deliberate mutation any serialization helper here performs, and
        /// it is what makes "every file FEMEX writes is versioned" true of models
        /// built in memory as well as of files round-tripped.
        /// </summary>
        public string ToJson()
        {
            SchemaVersion ??= CurrentSchemaVersion;
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        public static FemexModel FromJson(string json)
        {
            FemexModel? model = JsonSerializer.Deserialize<FemexModel>(json, JsonOptions);
            if (model is null)
                throw new JsonException("Deserialized FEMEX model was null.");
            return model;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, ToJson());
        }

        public static FemexModel Load(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        // Referential integrity lives in FemexModel.Validation.cs.
    }
}
