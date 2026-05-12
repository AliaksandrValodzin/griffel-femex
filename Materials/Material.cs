namespace griffel_femex.Materials
{
    public class Material
    {
        public string Name { get; set; }

        // Modulus of Elasticity (E) - e.g., in Pascals or N/mm²
        public double ModulusOfElasticity { get; set; }

        // Poisson's Ratio (ν) - typically between 0.0 and 0.5
        public double PoissonsRatio { get; set; }

        // Weight per unit volume (γ) - e.g., kN/m³
        public double UnitWeight { get; set; }

        // Characteristic Strength (f_k or σ) - e.g., Yield strength for steel or f'c for concrete
        public double Strength { get; set; }

        public Material(string name, double e, double nu, double unitWeight, double strength)
        {
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
