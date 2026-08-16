using System.ComponentModel;
using System.Text.Json.Serialization;

namespace griffel_femex.Materials
{
    /// <summary>
    /// An isotropic linear-elastic material, stored on the model and referenced by
    /// id from bars, plates, plate regions and mesh faces.
    /// </summary>
    public class Material : IIdentified
    {
        // Unique identifier (referenced by elements via MaterialId)
        public int Id { get; set; }

        // Optional round-trip identity. Null means this material has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Robot and ETABS key materials by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Name { get; set; }

        /// <summary>Modulus of Elasticity (E), in force per unit area.</summary>
        public double ModulusOfElasticity { get; set; }

        /// <summary>Poisson's Ratio (ν) — typically between 0.0 and 0.5.</summary>
        public double PoissonsRatio { get; set; }

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

        /// <summary>Characteristic strength — not needed for analysis, but useful for design.</summary>
        public double Strength { get; set; }

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
        /// Calculates the Shear Modulus (G) based on E and ν.
        /// Formula: G = E / (2 * (1 + ν))
        /// </summary>
        public double GetShearModulus()
        {
            return ModulusOfElasticity / (2 * (1 + PoissonsRatio));
        }
    }
}
