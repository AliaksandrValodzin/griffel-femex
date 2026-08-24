using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Materials
{
    /// <summary>
    /// The material's design values — what a checker needs to design a member,
    /// stated independently of any grade name.
    ///
    /// This is the materials half of the escape hatch
    /// <see cref="Geometry.Sections.SectionProperties"/> opened for sections in 1.5,
    /// and it is deliberately the same shape: one optional block of resolved
    /// numbers, so that a value crosses even when the receiver has never heard of
    /// the grade it came from. A receiver that does not recognise <c>"S355JR"</c>
    /// still gets 355 000 kPa of yield out of this block, exactly as a receiver
    /// without the Euronorm library still gets an IPE300's area out of the section
    /// one. FEMEX built that pattern for sections and did not build it for
    /// materials; this is the missing half.
    ///
    /// The 22 values are SAF's <c>Design properties</c> set exactly, in SAF's three
    /// groups. Nothing here says <i>which</i> group applies —
    /// <see cref="Material.Type"/> is that statement, and a block stated without one
    /// is warned about for that reason.
    ///
    /// Every field is <c>double?</c>, so "not stated" is distinct from zero — the
    /// same distinction <see cref="Geometry.Sections.SectionProperties"/> and
    /// <see cref="BoundaryConditions.Restraint.Stiffness"/> already draw.
    /// <see cref="FemexModel.Validate()"/> rejects a stated value that is not
    /// positive, zero included: a stated property is a claim about capacity, and
    /// zero is not a claim anything can be designed against.
    ///
    /// Each value is in the model's own force and length units, as every other
    /// number in FEMEX is — a strength is a force per unit area, and the strains
    /// below are dimensionless. Nothing here is in MPa because SAF's column heading
    /// says MPa; see <see cref="Units"/>.
    ///
    /// <b>Deliberately excluded:</b> partial safety factors, and anything else that
    /// belongs to a design process rather than to the material. γ_M is a statement
    /// about which code is being applied and at what limit state, and it changes
    /// between two checks of the same steel; the same argument keeps
    /// <c>Model.National code</c> out of FEMEX entirely.
    /// </summary>
    public class MaterialProperties : IExtensible
    {
        // ----- Steel -----

        /// <summary>Yield strength, fy.</summary>
        public double? Fy { get; set; }

        /// <summary>Ultimate tensile strength, fu.</summary>
        public double? Fu { get; set; }

        /// <summary>Specified minimum ultimate tensile strength, fu(minimum).</summary>
        public double? FuMinimum { get; set; }

        /// <summary>Ratio of expected to specified yield strength, Ry — dimensionless.</summary>
        public double? Ry { get; set; }

        /// <summary>Ratio of expected to specified tensile strength, Rt — dimensionless.</summary>
        public double? Rt { get; set; }

        // ----- Concrete -----

        /// <summary>Characteristic cylinder compressive strength, fck.</summary>
        public double? Fck { get; set; }

        /// <summary>Mean cylinder compressive strength, fcm.</summary>
        public double? Fcm { get; set; }

        /// <summary>Mean axial tensile strength, fctm.</summary>
        public double? Fctm { get; set; }

        /// <summary>5% fractile axial tensile strength, fctk,0.05.</summary>
        public double? Fctk05 { get; set; }

        /// <summary>95% fractile axial tensile strength, fctk,0.95.</summary>
        public double? Fctk95 { get; set; }

        /// <summary>Strain at peak stress for the parabola-rectangle law, εc2 — dimensionless.</summary>
        public double? EpsC2 { get; set; }

        /// <summary>Ultimate strain for the parabola-rectangle law, εcu2 — dimensionless.</summary>
        public double? EpsCu2 { get; set; }

        /// <summary>Strain at peak stress for the bi-linear law, εc3 — dimensionless.</summary>
        public double? EpsC3 { get; set; }

        /// <summary>Ultimate strain for the bi-linear law, εcu3 — dimensionless.</summary>
        public double? EpsCu3 { get; set; }

        // ----- Timber -----

        /// <summary>5% fractile modulus of elasticity parallel to the grain, E0,05.</summary>
        public double? E005 { get; set; }

        /// <summary>Mean modulus of elasticity perpendicular to the grain, E90,mean.</summary>
        public double? E90Mean { get; set; }

        /// <summary>Characteristic bending strength, fm,k.</summary>
        public double? Fmk { get; set; }

        /// <summary>Characteristic tensile strength parallel to the grain, ft,0,k.</summary>
        public double? Ft0k { get; set; }

        /// <summary>Characteristic tensile strength perpendicular to the grain, ft,90,k.</summary>
        public double? Ft90k { get; set; }

        /// <summary>Characteristic compressive strength parallel to the grain, fc,0,k.</summary>
        public double? Fc0k { get; set; }

        /// <summary>Characteristic compressive strength perpendicular to the grain, fc,90,k.</summary>
        public double? Fc90k { get; set; }

        /// <summary>Characteristic shear strength, fv,k.</summary>
        public double? Fvk { get; set; }

        // Parameterless constructor for serialization
        public MaterialProperties() { }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
