using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// The release of a single degree of freedom at a hinge.
    /// Captures "full or partial":
    ///  - Released = false -> rigid connection (no release)
    ///  - Released = true + ResidualStiffness == null -> full release (free)
    ///  - Released = true + ResidualStiffness has value -> partial release (residual spring stiffness)
    /// </summary>
    public class Release : IExtensible
    {
        // Whether this DOF is released
        public bool Released { get; set; }

        // Residual stiffness for a partial release; null = full release
        public double? ResidualStiffness { get; set; }

        public Release() { }

        public Release(bool released, double? residualStiffness = null)
        {
            Released = released;
            ResidualStiffness = residualStiffness;
        }

        // Convenience factories
        public static Release Rigid() => new Release(false);
        public static Release Full() => new Release(true);
        public static Release Partial(double residualStiffness) => new Release(true, residualStiffness);

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
