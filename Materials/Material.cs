namespace griffel_femex.Materials
{
    public class Material
    {
        // Unique identifier (referenced by elements via MaterialId)
        public int Id { get; set; }

        public string? Name { get; set; }

        // Modulus of Elasticity (E) - e.g., in Pascals or N/mm²
        public double ModulusOfElasticity { get; set; }

        // Poisson's Ratio (ν) - typically between 0.0 and 0.5
        public double PoissonsRatio { get; set; }

        // Weight per unit volume (γ) - e.g., kN/m³
        public double UnitWeight { get; set; }

        // Characteristic Strength (not needed for analysis, but useful for design)
        public double Strength { get; set; }

        // Parameterless constructor for serialization
        public Material() { }

        // Convenience constructor
        public Material(int id, string? name, double e, double nu, double unitWeight, double strength)
        {
            Id = id;
            Name = name;
            ModulusOfElasticity = e;
            PoissonsRatio = nu;
            UnitWeight = unitWeight;
            Strength = strength;
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
