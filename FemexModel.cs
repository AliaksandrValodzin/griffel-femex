using System.Text.Json;
using System.Text.Json.Serialization;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex
{
    /// <summary>
    /// The single JSON-serializable root of a FEMEX finite-element model.
    /// Holds the four data blocks (Geometry, Materials, Loads, Boundary
    /// Conditions) as flat lists of entities referenced by integer ids.
    /// </summary>
    public class FemexModel
    {
        // Optional metadata (length/force convention)
        public Units? Units { get; set; }

        // Geometry
        public List<Level> Levels { get; set; } = new List<Level>();
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Section> Sections { get; set; } = new List<Section>();
        public List<Bar> Bars { get; set; } = new List<Bar>();
        public List<Plate> Plates { get; set; } = new List<Plate>();

        // Materials
        public List<Material> Materials { get; set; } = new List<Material>();

        // Loads
        public List<LoadCase> LoadCases { get; set; } = new List<LoadCase>();
        public List<Load> Loads { get; set; } = new List<Load>();

        // Boundary conditions
        public List<Support> Supports { get; set; } = new List<Support>();
        public List<Hinge> Hinges { get; set; } = new List<Hinge>();

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

        // ----- Referential integrity -----

        /// <summary>
        /// Checks that every referenced id exists. Returns a human-readable
        /// message per problem; an empty sequence means the model is consistent.
        /// Not required for serialization.
        /// </summary>
        public IEnumerable<string> Validate()
        {
            var levelNumbers = new HashSet<int>(Levels.Select(l => l.LevelNumber));
            var nodeIds = new HashSet<int>(Nodes.Select(n => n.NodeNumber));
            var sectionIds = new HashSet<int>(Sections.Select(s => s.Id));
            var materialIds = new HashSet<int>(Materials.Select(m => m.Id));
            var loadCaseNumbers = new HashSet<int>(LoadCases.Select(c => c.Number));
            var elementIds = new HashSet<int>(Bars.Select(b => b.Id).Concat(Plates.Select(p => p.Id)));

            foreach (var node in Nodes)
            {
                if (!levelNumbers.Contains(node.LevelNumber))
                    yield return $"Node {node.NodeNumber} references unknown level {node.LevelNumber}.";
            }

            foreach (var bar in Bars)
            {
                if (!nodeIds.Contains(bar.StartNodeId))
                    yield return $"Bar {bar.Id} references unknown start node {bar.StartNodeId}.";
                if (!nodeIds.Contains(bar.EndNodeId))
                    yield return $"Bar {bar.Id} references unknown end node {bar.EndNodeId}.";
                if (!sectionIds.Contains(bar.SectionId))
                    yield return $"Bar {bar.Id} references unknown section {bar.SectionId}.";
                if (!materialIds.Contains(bar.MaterialId))
                    yield return $"Bar {bar.Id} references unknown material {bar.MaterialId}.";
            }

            foreach (var plate in Plates)
            {
                foreach (var nid in plate.NodeIds)
                {
                    if (!nodeIds.Contains(nid))
                        yield return $"Plate {plate.Id} references unknown node {nid}.";
                }
                if (!materialIds.Contains(plate.MaterialId))
                    yield return $"Plate {plate.Id} references unknown material {plate.MaterialId}.";
            }

            foreach (var load in Loads)
            {
                if (!loadCaseNumbers.Contains(load.LoadCaseNumber))
                    yield return $"Load '{load.Label}' references unknown load case {load.LoadCaseNumber}.";

                switch (load)
                {
                    case PointLoad pl when !nodeIds.Contains(pl.NodeNumber):
                        yield return $"Point load '{pl.Label}' references unknown node {pl.NodeNumber}.";
                        break;
                    case LinearLoad ll:
                        if (!nodeIds.Contains(ll.StartNode))
                            yield return $"Linear load '{ll.Label}' references unknown start node {ll.StartNode}.";
                        if (!nodeIds.Contains(ll.EndNode))
                            yield return $"Linear load '{ll.Label}' references unknown end node {ll.EndNode}.";
                        break;
                    case AreaLoad al:
                        foreach (var nid in al.NodeSequence)
                            if (!nodeIds.Contains(nid))
                                yield return $"Area load '{al.Label}' references unknown node {nid}.";
                        break;
                    case TemperatureLoad tl:
                        foreach (var eid in tl.ElementIds)
                            if (!elementIds.Contains(eid))
                                yield return $"Temperature load '{tl.Label}' references unknown element {eid}.";
                        break;
                }
            }

            foreach (var support in Supports)
            {
                foreach (var nid in support.NodeIds)
                    if (!nodeIds.Contains(nid))
                        yield return $"Support {support.Id} references unknown node {nid}.";
            }

            foreach (var hinge in Hinges)
            {
                if (!elementIds.Contains(hinge.ElementId))
                    yield return $"Hinge {hinge.Id} references unknown element {hinge.ElementId}.";
                foreach (var nid in hinge.NodeIds)
                    if (!nodeIds.Contains(nid))
                        yield return $"Hinge {hinge.Id} references unknown node {nid}.";
            }
        }
    }
}
