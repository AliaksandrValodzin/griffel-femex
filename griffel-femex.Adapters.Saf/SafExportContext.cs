using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Interop;
using griffel_femex.Materials;
using SAF.DataAccess.Models.Enums;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The state one export carries: the name each FEMEX object was given, the unit
    /// conversion, and the two lookups FEMEX does not hold that SAF requires.
    /// </summary>
    internal sealed class SafExportContext
    {
        private readonly SafNamer _namer = new SafNamer();
        private readonly Dictionary<Guid, string> _handles = new Dictionary<Guid, string>();
        private readonly Dictionary<int, int> _sectionMaterials = new Dictionary<int, int>();
        private readonly Dictionary<int, double> _thicknesses = new Dictionary<int, double>();

        public SafExportContext(FemexModel model, SafMessageLog log)
        {
            Model = model;
            Log = log;
            Units = SafUnits.Of(model.Units);
            Geometry = new SafGeometry(model);

            SystemOfUnits = Units.IsImperial ? ExcelSystemOfUnits.Imperial : ExcelSystemOfUnits.Metric;

            // SAF puts the material on the cross-section; FEMEX puts it on the bar.
            // So the section's material is whichever the model's members give it —
            // and where two members disagree, the first wins and the disagreement is
            // reported, because SAF cannot hold both.
            foreach (Bar bar in model.Bars)
            {
                if (bar.MaterialId == 0)
                    continue;

                if (_sectionMaterials.TryGetValue(bar.SectionId, out int existing))
                {
                    if (existing != bar.MaterialId)
                        log.Concept(SafLoss.SharedSectionMaterial);
                }
                else
                {
                    _sectionMaterials[bar.SectionId] = bar.MaterialId;
                }
            }

            foreach (SurfaceProperty property in model.SurfaceProperties)
            {
                if (property is ConstantThickness constant)
                    _thicknesses[property.Id] = constant.Thickness;
            }
        }

        public FemexModel Model { get; }

        public SafMessageLog Log { get; }

        public SafUnits Units { get; }

        public SafGeometry Geometry { get; }

        public ExcelSystemOfUnits SystemOfUnits { get; }

        /// <summary>The §5.3 uid to native-handle map, carried as data in the receipt.</summary>
        public IReadOnlyDictionary<Guid, string> Handles => _handles;

        public Dictionary<int, string> MaterialNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> SectionNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> NodeNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> BarNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> PlateNames { get; } = new Dictionary<int, string>();

        public Dictionary<(int Plate, int Region), string> RegionNames { get; } =
            new Dictionary<(int, int), string>();

        public Dictionary<int, string> LoadCaseNames { get; } = new Dictionary<int, string>();

        public Dictionary<int, string> LoadGroupNames { get; } = new Dictionary<int, string>();

        /// <summary>Groups invented on the export leg, one per load nature, keyed by that nature.</summary>
        public IReadOnlyDictionary<griffel_femex.Loads.LoadNature, string> SynthesisedGroups { get; set; } =
            new Dictionary<griffel_femex.Loads.LoadNature, string>();

        /// <summary>
        /// A name for one object on one sheet: what FEMEX called it, or a synthesised
        /// <c>{Kind}-{8 hex}</c> where FEMEX has no name property at all, made unique
        /// within the sheet because SAF treats a duplicate there as fatal.
        /// </summary>
        public string Name(string sheet, string? preferred, FemexEntity? kind, Guid? uid)
        {
            string candidate = preferred ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate) && kind.HasValue && uid.HasValue)
                candidate = NameSynthesis.For(kind.Value, uid.Value);

            if (string.IsNullOrWhiteSpace(candidate))
                candidate = sheet;

            return _namer.Unique(sheet, candidate);
        }

        public void Record(Guid? uid, string name)
        {
            if (uid.HasValue && uid.Value != Guid.Empty)
                _handles[uid.Value] = name;
        }

        public string NodeName(int nodeNumber)
        {
            return NodeNames.TryGetValue(nodeNumber, out string? name) ? name : string.Empty;
        }

        public string SectionName(int sectionId)
        {
            return SectionNames.TryGetValue(sectionId, out string? name) ? name : string.Empty;
        }

        public string BarName(int barId)
        {
            return BarNames.TryGetValue(barId, out string? name) ? name : string.Empty;
        }

        public string PlateName(int plateId)
        {
            return PlateNames.TryGetValue(plateId, out string? name) ? name : string.Empty;
        }

        public string? MaterialName(int? materialId)
        {
            return materialId.HasValue && MaterialNames.TryGetValue(materialId.Value, out string? name)
                ? name
                : null;
        }

        public string? MaterialNameFor(Section section)
        {
            return _sectionMaterials.TryGetValue(section.Id, out int materialId)
                ? MaterialName(materialId)
                : null;
        }

        /// <summary>
        /// A material for a section no member uses. SAF makes the column mandatory,
        /// so a section nothing references still has to name one; the model's first
        /// material is the least-wrong answer and it is reported rather than assumed.
        /// </summary>
        public string? FallbackMaterialName(string sectionName)
        {
            if (MaterialNames.Count == 0)
                return null;

            Log.Object(SafLoss.InventedSectionMaterial, new ObjectRef(FemexEntity.Section), sectionName);
            return MaterialNames.Values.First();
        }

        public double ThicknessOf(int? surfacePropertyId)
        {
            return surfacePropertyId.HasValue && _thicknesses.TryGetValue(surfacePropertyId.Value,
                                                                         out double thickness)
                ? thickness
                : 0.0;
        }

        /// <summary>
        /// Bars grouped back into the SAF members they came from.
        /// </summary>
        /// <remarks>
        /// This is what makes decision 10 resolve to <i>reversible</i>. A chorded
        /// curve arrives as a chain of bars, each pointing at the first through
        /// <c>ParentUid</c>; the export leg reads those pointers back and writes one
        /// member with the whole node run rather than eight. The arc-ness itself is
        /// still gone — FEMEX stores chords, not shapes — so what returns is a
        /// polyline through the same points, which is the difference the
        /// <c>ChordedCurve</c> message names.
        ///
        /// A bar whose <c>ParentUid</c> names something that is not another bar in
        /// this model — a former object, which the provenance rule explicitly
        /// tolerates — stands alone rather than being dropped.
        /// </remarks>
        /// <summary>
        /// Hinges grouped back into the SAF rows they came from, by the same
        /// provenance pointer the bar chains use.
        /// </summary>
        public List<List<Hinge>> HingeGroups()
        {
            var byUid = new Dictionary<Guid, Hinge>();
            foreach (Hinge hinge in Model.Hinges)
            {
                if (hinge.Uid.HasValue)
                    byUid[hinge.Uid.Value] = hinge;
            }

            var groups = new List<List<Hinge>>();
            var index = new Dictionary<Hinge, int>();

            foreach (Hinge hinge in Model.Hinges)
            {
                Hinge head = hinge;
                if (hinge.ParentUid.HasValue && byUid.TryGetValue(hinge.ParentUid.Value, out Hinge? parent) &&
                    !ReferenceEquals(parent, hinge))
                {
                    head = parent;
                }

                if (index.TryGetValue(head, out int position))
                {
                    groups[position].Add(hinge);
                }
                else
                {
                    index[head] = groups.Count;
                    groups.Add(new List<Hinge> { hinge });
                }
            }

            return groups;
        }

        public List<List<Bar>> BarChains()
        {
            var byUid = new Dictionary<Guid, Bar>();
            foreach (Bar bar in Model.Bars)
            {
                if (bar.Uid.HasValue)
                    byUid[bar.Uid.Value] = bar;
            }

            var chains = new List<List<Bar>>();
            var index = new Dictionary<Bar, int>();

            foreach (Bar bar in Model.Bars)
            {
                Bar head = bar;
                if (bar.ParentUid.HasValue && byUid.TryGetValue(bar.ParentUid.Value, out Bar? parent) &&
                    !ReferenceEquals(parent, bar))
                {
                    head = parent;
                }

                if (index.TryGetValue(head, out int position))
                {
                    chains[position].Add(bar);
                }
                else
                {
                    index[head] = chains.Count;
                    chains.Add(new List<Bar> { bar });
                }
            }

            return chains;
        }
    }
}
