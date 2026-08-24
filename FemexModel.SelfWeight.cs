using System.Text.Json.Serialization;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex
{
    /// <summary>
    /// Self-weight, executable: γ = ρ·g, a bar's γ·A and a plate's γ·t — the same
    /// argument <see cref="GetDesignEnvelope"/> and <see cref="TryGetLoadDirection"/>
    /// already made for their own rules. The 1.1 <c>unitWeight</c> migration lives
    /// here too, because converting γ into ρ needs the root's gravity and so cannot
    /// live on <see cref="Material"/>.
    ///
    /// Every helper is a lookup, mirroring <c>FemexModel.LocalAxes.cs</c>: nothing
    /// here changes the model, and an unresolvable reference is answered with false
    /// rather than an exception, because <see cref="Validate()"/> reports each of
    /// those cases in its own words.
    ///
    /// <b>Total model self-weight is deliberately absent.</b> It needs the
    /// overlapping priority regions of <see cref="PlateRegion"/> resolved into
    /// non-overlapping areas, and there is no polygon-boolean code here; the
    /// per-element intensities below are its input.
    /// </summary>
    public partial class FemexModel : IJsonOnDeserialized
    {
        // Which materials the 1.1 migration converted, and which stated both
        // spellings. Private fields, which System.Text.Json never serializes, so no
        // key can leak into a file. They record a property of the read rather than
        // of the model: a re-emitted file is 1.2 and carries a density, so it must
        // not warn again, and a model built in memory never migrates at all.
        private List<int>? _migratedUnitWeightMaterialIds;
        private List<int>? _bothDensitySpellingsMaterialIds;

        /// <summary>
        /// Runs from every deserialization entry point — <see cref="FromJson"/>,
        /// <see cref="Load"/>, and a bare <c>JsonSerializer.Deserialize</c> a
        /// consumer writes for itself — so no reader can skip a migration, and none
        /// of them can depend on where a key sits in the file relative to another:
        /// the unit-weight conversion needs <c>gravity</c> whatever order it was
        /// written in, and the load numbering needs <c>schemaVersion</c>. Explicitly
        /// implemented to keep it off the public surface.
        ///
        /// Every migration this build performs hangs here. They are independent and
        /// run in version order.
        /// </summary>
        void IJsonOnDeserialized.OnDeserialized()
        {
            // 1.1 → 1.2: γ becomes ρ. Lives here, next to the arithmetic it needs.
            MigrateLegacyUnitWeight();

            // 1.2 → 1.3: loads get the ids they never had. In FemexModel.Identity.cs.
            MigrateLegacyLoadIds();

            // 1.7 → 1.8: free-text units become enums. In FemexModel.Units.cs.
            MigrateLegacyUnits();
        }

        /// <summary>
        /// Converts each 1.1 <c>unitWeight</c> (γ) into a <see cref="Material.Density"/>
        /// (ρ = γ/g) through this model's own gravity, and records what it did for
        /// <see cref="Validate()"/> to report.
        ///
        /// The conversion preserves weight density exactly, in any unit system: ρ =
        /// γ/g on load and γ = ρ·g in <see cref="GetWeightDensity"/> read the same
        /// acceleration, so a 1.1 file's γ comes back to floating-point equality
        /// whatever units it was written in — the migration never has to know what
        /// those units were.
        ///
        /// A material stating both spellings keeps its density: the two cannot both
        /// be right and the newer one wins. A non-positive acceleration leaves the
        /// density at zero rather than producing an infinity;
        /// <see cref="Validate()"/> reports that acceleration as an error in its own
        /// right.
        /// </summary>
        private void MigrateLegacyUnitWeight()
        {
            foreach (var material in Materials)
            {
                if (!material.TryTakeLegacyUnitWeight(out double unitWeight))
                    continue;

                if (material.Density != 0.0)
                {
                    (_bothDensitySpellingsMaterialIds ??= new List<int>()).Add(material.Id);
                    continue;
                }

                if (Gravity.Acceleration > 0.0)
                    material.Density = unitWeight / Gravity.Acceleration;

                (_migratedUnitWeightMaterialIds ??= new List<int>()).Add(material.Id);
            }
        }

        // ----- Lookups -----

        /// <summary>
        /// The unit vector gravity acts along, in global coordinates — the one place
        /// the model's <see cref="Gravity"/> direction is read.
        /// <see cref="Vector3d.Zero"/> when <c>dx</c>, <c>dy</c> and <c>dz</c> are
        /// all zero, which <see cref="Validate()"/> reports as an error.
        ///
        /// The vector's own magnitude is discarded: how strong gravity is lives in
        /// <see cref="Gravity.Acceleration"/> alone, so an author who writes
        /// <c>dz: -9.80665</c> as well gets 9.80665, not 96.
        /// </summary>
        public Vector3d GetGravityDirection()
        {
            return new Vector3d(Gravity.Dx, Gravity.Dy, Gravity.Dz).Normalized();
        }

        /// <summary>
        /// Weight density γ = ρ·g for a material: force per unit volume, in the
        /// model's own units. <c>0.0</c> for a material that is not in the model —
        /// following <see cref="GetTotalFactor"/>'s "0.0 when the case is absent",
        /// and the dangling reference is already an error
        /// <see cref="Validate()"/> reports.
        /// </summary>
        public double GetWeightDensity(int materialId)
        {
            Material? material = Materials.Find(m => m.Id == materialId);
            return material is null ? 0.0 : material.Density * Gravity.Acceleration;
        }

        /// <summary>
        /// A bar's self-weight as a force per unit length in global coordinates:
        /// γ·A along <see cref="GetGravityDirection"/>. False when the bar, its
        /// section or its material is unknown, or when gravity has no direction.
        ///
        /// A is <see cref="Geometry.Sections.Section.GetArea"/> and not
        /// <c>CalculateArea()</c>: a section that states its own area weighs by the
        /// tabulated number rather than the idealised one.
        ///
        /// <b>Unfactored</b> — the weight itself, at gravity 1.0. A load case applies
        /// its own <see cref="LoadCase.SelfWeightFactor"/>; multiplying here would
        /// make the answer depend on which case was asking.
        /// </summary>
        public bool TryGetBarSelfWeightPerLength(int barId, out Vector3d forcePerLength)
        {
            forcePerLength = Vector3d.Zero;

            Bar? bar = Bars.Find(b => b.Id == barId);
            if (bar is null)
                return false;

            Section? section = Sections.Find(s => s.Id == bar.SectionId);
            if (section is null)
                return false;

            if (!Materials.Exists(m => m.Id == bar.MaterialId))
                return false;

            Vector3d direction = GetGravityDirection();
            if (direction.Length <= 0.0)
                return false;

            forcePerLength = direction * (GetWeightDensity(bar.MaterialId) * section.GetArea());
            return true;
        }

        /// <summary>
        /// The base panel's self-weight per unit area — what the plate contributes
        /// outside every region. See the region overload.
        /// </summary>
        public bool TryGetPlateSelfWeightPerArea(int plateId, out Vector3d forcePerArea)
        {
            return TryGetPlateSelfWeightPerArea(plateId, null, out forcePerArea);
        }

        /// <summary>
        /// A plate's or one of its regions' self-weight as a force per unit area in
        /// global coordinates: γ·t along <see cref="GetGravityDirection"/>, with
        /// <paramref name="regionId"/> null meaning the base panel.
        ///
        /// The result is a global force vector and not a scalar because a plate's
        /// natural scalar axis is its normal and self-weight is not along it: a wall
        /// standing in the y = 0 plane has a horizontal normal and a vertical weight,
        /// and a scalar plus an implied axis would report that weight as a lateral
        /// pressure. <c>‖forcePerArea‖</c> is γ·t for anyone who wants the scalar.
        ///
        /// An <see cref="PlateRegionKind.Opening"/> or a
        /// <see cref="PlateRegionKind.LoadOnly"/> area weighs nothing: true with
        /// <see cref="Vector3d.Zero"/>, which is a definite answer rather than a
        /// refusal, so a caller summing regions need not branch. False is reserved
        /// for genuinely unanswerable — an unknown plate or region, a governing
        /// surface property or material that does not resolve, or gravity with no
        /// direction.
        ///
        /// <b>Unfactored</b>, exactly as for a bar.
        /// </summary>
        public bool TryGetPlateSelfWeightPerArea(int plateId, int? regionId, out Vector3d forcePerArea)
        {
            forcePerArea = Vector3d.Zero;

            Plate? plate = Plates.Find(p => p.Id == plateId);
            if (plate is null)
                return false;

            PlateRegion? region = null;
            if (regionId.HasValue)
            {
                region = plate.Regions.Find(r => r.Id == regionId.Value);
                if (region is null)
                    return false;
            }

            var (kind, surfacePropertyId, materialId) = GetEffectiveProperties(plate, region);

            // Neither generates mass: a definite zero, not a refusal.
            if (kind != PlateRegionKind.Structural)
                return true;

            if (surfacePropertyId is null || materialId is null)
                return false;

            SurfaceProperty? surface = SurfaceProperties.Find(s => s.Id == surfacePropertyId.Value);
            if (surface is null || !Materials.Exists(m => m.Id == materialId.Value))
                return false;

            Vector3d direction = GetGravityDirection();
            if (direction.Length <= 0.0)
                return false;

            forcePerArea = direction * (GetWeightDensity(materialId.Value) * surface.GetNominalThickness());
            return true;
        }

        /// <summary>
        /// Every load case that carries some of the structure's own weight — a
        /// non-zero <see cref="LoadCase.SelfWeightFactor"/> — in list order. Empty
        /// for a model whose weight is nowhere, which <see cref="Validate()"/> warns
        /// about.
        /// </summary>
        public IEnumerable<LoadCase> GetSelfWeightCases()
        {
            var cases = new List<LoadCase>();
            foreach (var loadCase in LoadCases)
                if (loadCase.SelfWeightFactor != 0.0)
                    cases.Add(loadCase);

            return cases;
        }

        /// <summary>
        /// The region-inheritance rule, stated once: a region takes the plate's
        /// surface property and material wherever it leaves its own null, and its own
        /// <see cref="PlateRegion.Kind"/> always. A null region is the base panel, so
        /// the plate answers for itself.
        ///
        /// Both <see cref="ValidatePlates"/> and the self-weight helpers call this,
        /// so validation and the arithmetic cannot drift apart — the same argument
        /// that made <c>TryGetNewellNormal</c> shared between the planarity check and
        /// the local-axis convention.
        /// </summary>
        private static (PlateRegionKind Kind, int? SurfacePropertyId, int? MaterialId) GetEffectiveProperties(
            Plate plate, PlateRegion? region)
        {
            if (region is null)
                return (plate.Kind, plate.SurfacePropertyId, plate.MaterialId);

            return (region.Kind,
                    region.SurfacePropertyId ?? plate.SurfacePropertyId,
                    region.MaterialId ?? plate.MaterialId);
        }
    }
}
