using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// The section's resolved numbers — what a solver needs to build a member, and
    /// what a checker needs to design one — stated independently of any shape.
    ///
    /// This is the format's escape hatch. A section FEMEX has no class for is
    /// written as a <see cref="GenericSection"/> carrying these, so it crosses by
    /// its stiffness rather than being lost; a section that <i>does</i> have a shape
    /// carries them too, and where it does <b>the stated number wins</b>. A
    /// tabulated IPE300 area is 5.381e-3 m² where the parametric formula over the
    /// same four dimensions gives 5.188e-3, about 3.6% low, because the tabulated
    /// number includes root fillets that no idealisation carries. The stated one is
    /// the measured one. <see cref="Section.GetArea"/> is that rule, executable.
    ///
    /// Every field is <c>double?</c>, so "not stated" is distinct from zero — the
    /// same distinction <see cref="BoundaryConditions.Restraint.Stiffness"/> already
    /// draws. <see cref="FemexModel.Validate()"/> rejects a stated value that is not
    /// positive, zero included: a stated property is a claim about stiffness, and
    /// zero is not a claim a solver can build with.
    ///
    /// The axes are <see cref="Geometry.Bar"/>'s local y and z, which
    /// <c>FemexModel.TryGetBarLocalAxes</c> makes executable — nothing is invented
    /// here.
    /// </summary>
    public class SectionProperties : IExtensible
    {
        // ----- Analysis: what a solver needs -----

        /// <summary>Cross-sectional area, A.</summary>
        public double? Area { get; set; }

        /// <summary>Shear area along local y, Ay.</summary>
        public double? ShearAreaY { get; set; }

        /// <summary>Shear area along local z, Az.</summary>
        public double? ShearAreaZ { get; set; }

        /// <summary>Second moment of area about local y, Iy.</summary>
        public double? Iy { get; set; }

        /// <summary>Second moment of area about local z, Iz.</summary>
        public double? Iz { get; set; }

        /// <summary>Torsion constant, J — SAF's <c>It</c>, Robot's <c>I_BSDV_IX</c>.</summary>
        public double? J { get; set; }

        // ----- Design: what SAF carries and a checker wants -----

        /// <summary>Warping constant, Iw.</summary>
        public double? Iw { get; set; }

        /// <summary>Elastic section modulus about local y, Wel,y.</summary>
        public double? Wely { get; set; }

        /// <summary>Elastic section modulus about local z, Wel,z.</summary>
        public double? Welz { get; set; }

        /// <summary>Plastic section modulus about local y, Wpl,y.</summary>
        public double? Wply { get; set; }

        /// <summary>Plastic section modulus about local z, Wpl,z.</summary>
        public double? Wplz { get; set; }

        // Parameterless constructor for serialization
        public SectionProperties() { }

        // Convenience constructor for the four an analysis actually reads; the
        // design group is set through the initializer.
        public SectionProperties(double? area, double? iy = null, double? iz = null, double? j = null)
        {
            Area = area;
            Iy = iy;
            Iz = iz;
            J = j;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
