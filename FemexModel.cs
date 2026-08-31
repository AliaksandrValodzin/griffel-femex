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
    public partial class FemexModel : IExtensible
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
        /// it is null or when it is a version this build has migrated, so every file
        /// FEMEX writes is versioned and says truthfully which format it is in. A
        /// version this build does not recognise is left alone.
        /// </summary>
        public string? SchemaVersion { get; set; }

        /// <summary>
        /// The version this build writes and understands. 1.0 is the unstamped
        /// format that had no load directions; 1.1 added them, together with
        /// <c>LinearLoad.BarId</c> and this field; 1.2 added self-weight — the
        /// <see cref="Gravity"/> block, <see cref="LoadCase.SelfWeightFactor"/>, and
        /// <c>Material.Density</c> replacing 1.1's <c>unitWeight</c>; 1.3 added
        /// round-trip identity — the optional <see cref="IIdentified.Uid"/> on every
        /// authored entity, and <c>Load.Id</c>, which a load had never had; 1.4
        /// added file provenance — the <see cref="Metadata"/> block — and made every
        /// serializable type <see cref="IExtensible"/>, so a member from a schema
        /// this build has never heard of is preserved and reported instead of
        /// silently dropped; 1.5 added the section escape hatch — the optional
        /// <see cref="Geometry.Sections.SectionProperties"/> block, the
        /// geometry-less <see cref="Geometry.Sections.GenericSection"/>, and
        /// <c>Section.GetArea()</c> — so a shape FEMEX does not model crosses by its
        /// stiffness instead of being lost; 1.6 added steel — five parametric shapes
        /// (<c>ishape</c>, <c>channel</c>, <c>angle</c>, <c>box</c>, <c>pipe</c>) and
        /// the optional <see cref="Geometry.Sections.SectionCatalogue"/> block, which
        /// names a profile in the library it came out of; 1.7 completed the material
        /// — <see cref="Materials.Material.Type"/> and <c>Quality</c>, the two
        /// columns SAF marks mandatory and FEMEX had nowhere to put; a stated
        /// <c>ShearModulus</c>, authoritative over the isotropic quotient;
        /// <c>ThermalExpansion</c>, without which a temperature load is a number
        /// nothing can turn into a strain; and the optional
        /// <see cref="Materials.MaterialProperties"/> block, the design-value escape
        /// hatch 1.5 built for sections and not for materials; 1.8 typed the unit
        /// convention and gave a restraint a direction — the five
        /// <see cref="griffel_femex.Units"/> enums replacing 1.7's free text, in
        /// which <c>"length": "banana"</c> round-tripped clean; <c>lengthUnit</c> and
        /// <c>forceUnit</c> as new keys, the format's <b>first renamed keys</b>, with
        /// the old spellings read once and migrated; <c>Restraint.Sense</c>, which
        /// takes an uplift-free bearing and a tension-only tie from three of SAF's
        /// eight translation states to seven; and, in documentation alone, what a
        /// <c>Restraint.Stiffness</c> is measured against for each
        /// <see cref="BoundaryConditions.SupportTarget"/>; 1.9 gave loads the three
        /// things a real SAF workbook showed they were missing — the
        /// <see cref="Loads.LoadGroup"/> a case belongs to, which carries the
        /// <see cref="Loads.LoadGroupRelation"/> a <see cref="Loads.LoadNature"/>
        /// cannot express; <see cref="Geometry.LoadDistribution"/> on the
        /// <i>panel</i>, so a one-way slab stops crossing as a two-way one; and
        /// signed <c>GradientY</c>/<c>GradientZ</c> on
        /// <see cref="Loads.TemperatureLoad"/>, replacing a
        /// <c>gradientPerDepth</c> that carried no sign convention at all — plus a
        /// position along a member for a point or line load, and
        /// <see cref="IIdentified.ParentUid"/>, the provenance pointer that makes a
        /// chorded arc reversible rather than merely lossy; 1.10 did the same for
        /// members — <see cref="Geometry.BarBehaviour"/>, populated on every row of
        /// the reference workbook and axial-only on four fifths of them;
        /// <see cref="Geometry.BarAlignment"/>, the system line, which is mandatory
        /// in SAF and is <i>not</i> always the centroid;
        /// <see cref="Geometry.BarEccentricity"/>, which keeps SAF's split between
        /// the offset that moves the picture and the offset that moves the answer;
        /// <c>Bar.EndSectionId</c> for a single linear taper, which downgrades a
        /// varying member from silently wrong to reportedly approximate; and the
        /// position along a member for a support or a hinge, completing what 1.9
        /// began for loads; and 1.11 gave a line load the second host it always
        /// needed — <see cref="Loads.LinearLoad.PlateId"/> and
        /// <c>LinearLoad.RegionId</c>, beside the <c>BarId</c> 1.9 added, so that a
        /// load running along a panel's contour edge can state a local direction and
        /// a partial extent at all. Additive, and no migration: the edge is already
        /// named by the load's two nodes, exactly as
        /// <see cref="BoundaryConditions.Hinge"/> names a hinged one, so nothing that
        /// crossed before is written differently now.
        /// </summary>
        public const string CurrentSchemaVersion = "1.11";

        /// <summary>
        /// Every version this build can read, current one included. A matched list
        /// and deliberately <b>not</b> a comparison rule: FEMEX has no ordering
        /// policy over versions, and inventing one here would be inventing behaviour
        /// for versions that do not exist yet. A version in this list is one whose
        /// meaning this build knows and, where it differs, has migrated;
        /// anything else is read as the current version and warned about.
        /// </summary>
        private static readonly string[] ReadableSchemaVersions = { "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10", CurrentSchemaVersion };

        /// <summary>
        /// Who wrote this file, with what, for which project and when — declared
        /// immediately after <see cref="SchemaVersion"/>, so it is the second key in
        /// the file, ahead of <see cref="Units"/> and <see cref="Gravity"/>.
        ///
        /// Nullable with no initializer, like <see cref="Units"/> and unlike
        /// <see cref="Gravity"/>: gravity is <i>consumed</i>, by the 1.1 migration
        /// and the self-weight helpers, and this is pure annotation that nothing in
        /// the library computes with. A model that says nothing about its
        /// provenance omits the key entirely rather than writing an empty block.
        ///
        /// <see cref="ToJson"/> does not stamp it; see <see cref="FileMetadata"/>
        /// for why.
        /// </summary>
        public FileMetadata? Metadata { get; set; }

        // Optional metadata: what the numbers in this model are in. Typed from 1.8.
        public Units? Units { get; set; }

        /// <summary>
        /// Which way gravity acts in this model, and how strong it is — the third key
        /// in the file, beside the unit convention, because it is the same class of
        /// statement as "Z is up".
        ///
        /// Non-nullable with an initializer, unlike <see cref="Units"/>: units are
        /// pure annotation that nothing in the library computes with, whereas gravity
        /// is <i>consumed</i> — by the 1.1 migration and by the self-weight
        /// helpers — and a nullable one would force every consumer to invent the
        /// default at the point of use.
        /// </summary>
        public Gravity Gravity { get; set; } = new Gravity();

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
        //
        // Groups are declared before cases because a case references a group, which
        // is the order every other block in this file already follows. A root list
        // like every other one — non-nullable and always written, so a model with no
        // groups writes "loadGroups": [] exactly as it already writes "grids": [].
        public List<LoadGroup> LoadGroups { get; set; } = new List<LoadGroup>();

        public List<LoadCase> LoadCases { get; set; } = new List<LoadCase>();
        public List<Load> Loads { get; set; } = new List<Load>();
        public List<LoadCombination> LoadCombinations { get; set; } = new List<LoadCombination>();

        // Boundary conditions
        public List<Support> Supports { get; set; } = new List<Support>();
        public List<Hinge> Hinges { get; set; } = new List<Hinge>();

        // The generated finite-element mesh of the plates. Null until the model has
        // been meshed, and omitted from the JSON entirely while it is.
        public FemexMesh? Mesh { get; set; }

        /// <summary>
        /// Root-level members this build does not know; see <see cref="IExtensible"/>.
        /// Declared last, which costs nothing: System.Text.Json writes extension
        /// data after every declared property whatever position it is declared in.
        ///
        /// <c>UnmappedMemberHandling</c> — what the interop review asked for — is
        /// System.Text.Json <b>8.0</b>, which both legs reach since the
        /// <c>netstandard2.0;net8.0</c> multi-target. It stays unset all the same,
        /// and now by choice rather than by the framework: it refuses the file,
        /// where extension data preserves the payload and reports it.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }

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
        /// <see cref="CurrentSchemaVersion"/> first. That stamp is the one deliberate
        /// mutation any serialization helper here performs, and it is what makes
        /// "every file FEMEX writes is versioned" true of models built in memory as
        /// well as of files round-tripped.
        ///
        /// It restamps, not merely fills in. A model with no version gets one; so
        /// does one carrying any version in <see cref="ReadableSchemaVersions"/>,
        /// because reading such a file migrated it and what is about to be written is
        /// the current format — a 1.1 file re-emitted as <c>"1.1"</c> while carrying
        /// <c>density</c> would be a file that lies about itself. A version this
        /// build does not recognise is left exactly as it was: it was not migrated,
        /// so it is not ours to restate.
        /// </summary>
        public string ToJson()
        {
            if (SchemaVersion is null || Array.IndexOf(ReadableSchemaVersions, SchemaVersion) >= 0)
                SchemaVersion = CurrentSchemaVersion;

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
