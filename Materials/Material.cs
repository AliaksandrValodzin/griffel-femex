using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Materials
{
    /// <summary>
    /// A linear-elastic material, stored on the model and referenced by id from
    /// bars, plates, plate regions and mesh faces.
    ///
    /// Isotropic in everything the analysis reads, with one deliberate exception:
    /// <see cref="ShearModulus"/> may be stated and need not equal E/(2(1+ν)), which
    /// is the one place a real material is allowed to contradict the isotropic
    /// relation. It is not a full orthotropic model and does not pretend to be —
    /// there is one E, one ν and one α.
    ///
    /// From 1.7 the material also carries what it <i>is</i> — <see cref="Type"/> and
    /// <see cref="Quality"/> — and what a checker designs against, in
    /// <see cref="Properties"/>. The first two are the columns SAF marks mandatory
    /// and FEMEX had no home for; the third is the escape hatch
    /// <see cref="Geometry.Sections.SectionProperties"/> opened for sections in 1.5,
    /// applied to the other half of the pair.
    /// </summary>
    public class Material : IIdentified, IExtensible
    {
        // Unique identifier (referenced by elements via MaterialId)
        public int Id { get; set; }

        // Optional round-trip identity. Null means this material has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this material was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        // Robot and ETABS key materials by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Name { get; set; }

        /// <summary>
        /// What family of material this is. Nullable with no initializer, so a file
        /// written before 1.7 gains nothing and no default is invented for it — this
        /// is a statement about the material, and <see cref="MaterialType.Other"/> is
        /// a different statement from silence. <see cref="FemexModel.Validate()"/>
        /// warns about the silence, because an exporter has to invent something.
        /// </summary>
        public MaterialType? Type { get; set; }

        /// <summary>
        /// The grade designation as its code writes it — <c>"S235"</c>,
        /// <c>"C25/30"</c>, <c>"GL24h"</c> — and deliberately distinct from
        /// <see cref="Name"/>, which is the free label Robot and ETABS key materials
        /// by and which an author is free to call <c>"slab concrete"</c>.
        ///
        /// Free text where <see cref="Type"/> is an enum, for the reason
        /// <see cref="MaterialType"/> states: the set of grades is national, open and
        /// still growing. A quality stated with no type is warned about — a grade
        /// names nothing without the code family it belongs to.
        /// </summary>
        public string? Quality { get; set; }

        /// <summary>Modulus of Elasticity (E), in force per unit area.</summary>
        public double ModulusOfElasticity { get; set; }

        /// <summary>Poisson's Ratio (ν) — typically between 0.0 and 0.5.</summary>
        public double PoissonsRatio { get; set; }

        /// <summary>
        /// Shear modulus (G), in force per unit area. Null means the material states
        /// none and <see cref="GetShearModulus"/> derives one from E and ν.
        ///
        /// Worth stating because the isotropic quotient is not always the truth.
        /// Timber's G is nothing like E/(2(1+ν)), and SAF carries <c>G modulus</c> as
        /// its own column beside <c>E modulus</c> and <c>Poisson Coefficient</c>
        /// precisely so that it can disagree with them.
        /// </summary>
        public double? ShearModulus { get; set; }

        /// <summary>
        /// Mass per unit volume (ρ). Weight density γ = ρ·g, with g from the model's
        /// <see cref="FemexModel.Gravity"/>; <c>FemexModel.GetWeightDensity</c> is
        /// that product, executable.
        ///
        /// In mass units consistent with the model's force and length units, where
        /// mass = force·time²/length — so with kN and m that is <b>tonnes</b>, and
        /// concrete is 2.5, not 2500. Replaces the 1.1 field <c>unitWeight</c>,
        /// which was γ directly; the two differ by a factor of g.
        /// </summary>
        public double Density { get; set; }

        /// <summary>
        /// The 1.1 spelling, γ, bound on read so nothing is silently lost.
        /// Deliberately <b>getter-less</b>: System.Text.Json can never write it back,
        /// so a 1.2 file cannot contain it. The migration divides it by the model's
        /// gravity acceleration into <see cref="Density"/> and clears it.
        /// </summary>
        [JsonPropertyName("unitWeight")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public double UnitWeight
        {
            set
            {
                _legacyUnitWeight = value;
                _hasLegacyUnitWeight = true;
            }
        }

        private double _legacyUnitWeight;
        private bool _hasLegacyUnitWeight;

        /// <summary>
        /// Coefficient of thermal expansion (α), in 1/K. Null means the material
        /// states none.
        ///
        /// This is what makes <see cref="Loads.TemperatureLoad"/> mean anything: a
        /// temperature change is not a load until something turns it into a strain,
        /// and α is that something. Without it a thermal load arrives at the
        /// receiving program as a number it cannot use — an internal inconsistency,
        /// not just an omission, which is why
        /// <see cref="FemexModel.Validate()"/> warns when a temperature load's
        /// elements resolve to a material that leaves this null.
        /// </summary>
        public double? ThermalExpansion { get; set; }

        /// <summary>
        /// Characteristic strength — not needed for analysis, but useful for design.
        ///
        /// Kept because removing it would break every consumer written against 1.1,
        /// but from 1.7 on <see cref="Properties"/> is where a design value belongs:
        /// it says <i>which</i> strength, and this field never did.
        /// </summary>
        public double Strength { get; set; }

        /// <summary>
        /// The material's design values, stated independently of its grade name.
        /// Null means it states none, and <see cref="Quality"/> is all a receiver has
        /// to look the grade up by. See <see cref="MaterialProperties"/>.
        /// </summary>
        public MaterialProperties? Properties { get; set; }

        // Parameterless constructor for serialization
        public Material() { }

        /// <summary>
        /// Convenience constructor. <paramref name="density"/> is ρ, <b>mass</b> per
        /// unit volume — 2.5 for concrete in a kN/m model, not 25. It was γ in 1.1
        /// and the two differ by a factor of g, so a positional call written against
        /// 1.1 still compiles and now means something a thousandth of the size the
        /// author intended. The <c>schemaVersion</c> bump is the only other signal
        /// that this changed.
        /// </summary>
        public Material(int id, string? name, double e, double nu, double density, double strength)
        {
            Id = id;
            Name = name;
            ModulusOfElasticity = e;
            PoissonsRatio = nu;
            Density = density;
            Strength = strength;
        }

        /// <summary>
        /// Hands the pending 1.1 unit weight to the migration and forgets it, so the
        /// conversion can only ever happen once. False when the file carried no
        /// <c>unitWeight</c> at all.
        /// </summary>
        internal bool TryTakeLegacyUnitWeight(out double unitWeight)
        {
            unitWeight = _legacyUnitWeight;

            if (!_hasLegacyUnitWeight)
                return false;

            _legacyUnitWeight = 0.0;
            _hasLegacyUnitWeight = false;
            return true;
        }

        /// <summary>
        /// The shear modulus to build a member with: the stated one where the
        /// material carries it, the isotropic quotient E/(2(1+ν)) otherwise. Timber's
        /// measured G is nothing like that quotient, so where both exist the stated
        /// one is the measured one and wins — the identical rule
        /// <see cref="Geometry.Sections.Section.GetArea"/> already states for area.
        /// </summary>
        public double GetShearModulus()
        {
            return ShearModulus ?? ModulusOfElasticity / (2 * (1 + PoissonsRatio));
        }

        // Members this build does not know; see IExtensible. The 1.1 spelling
        // unitWeight is not one of them — it is a declared property above, so the
        // migration is untouched by extension data.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
