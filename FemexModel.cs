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

        public string ToJson()
        {
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
