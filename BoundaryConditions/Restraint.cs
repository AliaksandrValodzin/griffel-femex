using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// The restraint of a single degree of freedom: how stiff it is, and — from
    /// 1.8 — which way it acts.
    ///
    ///  - Fixed = true  -> infinite stiffness (fully restrained)
    ///  - Fixed = false + Stiffness == null -> free
    ///  - Fixed = false + Stiffness has value -> finite (spring) stiffness
    ///
    /// Crossed with <see cref="Sense"/>, that reaches <b>seven of SAF's eight</b>
    /// translation states, where 1.7 reached three:
    ///
    /// <list type="table">
    /// <item><term>Rigid</term><description><c>Fixed = true</c></description></item>
    /// <item><term>Free</term><description><c>Fixed = false</c>, <c>Stiffness = null</c></description></item>
    /// <item><term>Flexible</term><description><c>Stiffness = k</c></description></item>
    /// <item><term>Compression only</term><description><c>Fixed = true</c>, <c>Sense = CompressionOnly</c></description></item>
    /// <item><term>Tension only</term><description><c>Fixed = true</c>, <c>Sense = TensionOnly</c></description></item>
    /// <item><term>Flexible compression only</term><description><c>Stiffness = k</c>, <c>Sense = CompressionOnly</c></description></item>
    /// <item><term>Flexible tension only</term><description><c>Stiffness = k</c>, <c>Sense = TensionOnly</c></description></item>
    /// <item><term>Non linear</term><description><b>unmapped</b></description></item>
    /// </list>
    ///
    /// <b>The eighth is deliberately unmapped.</b> SAF's <c>Non linear</c> is not a
    /// state at all — it is a reference to a stiffness curve, a table of force
    /// against displacement living outside this class, and representing it would
    /// mean adding a curve type to FEMEX rather than a value to an enum. An adapter
    /// reports it <i>Approximated</i> — the nearest linear state — or <i>Dropped</i>,
    /// per <c>FEMEX_Adapters.md</c> §4.3. Recorded here rather than left implicit,
    /// because "seven of eight" is only an honest claim if the eighth is named.
    ///
    /// This qualifies <c>FEMEX_Interop_Review.md</c> §3.5, which called the six-DOF
    /// pattern <i>"correct, and correctly factored"</i>. True of the shape — a state
    /// and a stiffness per DOF, reused across point, line and area targets — and it
    /// was the <i>value set</i> that was short.
    /// </summary>
    public class Restraint : IExtensible
    {
        // Infinite stiffness (fully restrained)
        public bool Fixed { get; set; }

        /// <summary>
        /// Finite spring stiffness; null = free (only meaningful when
        /// <see cref="Fixed"/> is false).
        ///
        /// <b>What this number is in depends on the support's
        /// <see cref="Support.Target"/></b>, and until 1.8 FEMEX did not say — which
        /// <c>FEMEX_SAF_Fit.md</c> §4 item 7 records as two adapters reading the same
        /// file and differing by a factor of the slab area, neither wrong against a
        /// spec that did not exist. It says now:
        ///
        /// <list type="bullet">
        /// <item><description><see cref="SupportTarget.Point"/> — a <b>total spring</b>:
        /// force per unit displacement (force/length), or moment per radian for a
        /// rotational DOF.</description></item>
        /// <item><description><see cref="SupportTarget.Linear"/> — <b>per unit length</b>
        /// of the supported line: force/length per unit displacement, so
        /// force/length².</description></item>
        /// <item><description><see cref="SupportTarget.Area"/> — a <b>bedding modulus
        /// per unit area</b>: force/length² per unit displacement, so force/length³.
        /// This is SAF's Winkler <c>C1</c> and a geotechnical report's modulus of
        /// subgrade reaction, in the model's own units rather than SAF's
        /// MN/m³.</description></item>
        /// </list>
        ///
        /// All three in the model's own force and length units, as every other number
        /// in FEMEX is — see <see cref="Units"/>. Which is why an area support stating
        /// a stiffness in a model that states no units is warned about by
        /// <see cref="FemexModel.Validate()"/>: a bedding modulus is a number of a
        /// dimension no reader can guess, and three orders of magnitude separate
        /// kN/m³ from kN/mm³.
        ///
        /// <b>SAF's Pasternak <c>C2</c> is deliberately unmapped.</b> The
        /// <c>StructuralSurfaceConnection</c> pair is Winkler-Pasternak: <c>C1x/y/z</c>
        /// resist displacement, <c>C2x/y</c> resist the <i>shear</i> of the subsoil and
        /// so couple neighbouring points, which no per-DOF spring can express. An
        /// adapter reports it <i>Dropped</i>. Carrying it would mean a subsoil type of
        /// its own, and nothing in <c>FEMEX_Adapters.md</c>'s target list beyond SAF
        /// asks for one.
        /// </summary>
        public double? Stiffness { get; set; }

        /// <summary>
        /// Which way this restraint acts; null means both, which is what every
        /// restraint written before 1.8 means and the only reading available for one.
        /// See <see cref="RestraintSense"/>.
        ///
        /// Nullable with no initializer, so a 1.7 file re-saved as 1.8 gains not one
        /// byte — and so that an author writing <c>Both</c> is saying something a
        /// silent file is not.
        ///
        /// <b>Meaningless on a rotational DOF, and representable there anyway.</b>
        /// <see cref="Support"/> applies this class uniformly across all six degrees
        /// of freedom, and the factoring that makes the shape reusable is exactly what
        /// stops the type from forbidding it — so <c>Rx.Sense = CompressionOnly</c>
        /// parses, serializes, and describes nothing: a moment has no compression
        /// side. <see cref="FemexModel.Validate()"/> warns where the schema does not
        /// forbid, which is the line this repository draws everywhere — nothing the
        /// format forbids is ever only a warning, and this the format permits.
        /// </summary>
        public RestraintSense? Sense { get; set; }

        public Restraint() { }

        public Restraint(bool @fixed, double? stiffness = null)
        {
            Fixed = @fixed;
            Stiffness = stiffness;
        }

        // Convenience factories
        public static Restraint FixedDof() => new Restraint(true);
        public static Restraint Free() => new Restraint(false);
        public static Restraint Spring(double stiffness) => new Restraint(false, stiffness);

        /// <summary>
        /// An uplift-free bearing: rigid into the support, free out of it — SAF's
        /// <c>Compression only</c>, or <c>Flexible compression only</c> where a
        /// stiffness is given. This is the case <see cref="Sense"/> exists for: a pad
        /// bearing imported as <see cref="FixedDof"/> resists an uplift the real one
        /// cannot, and the model solves.
        /// </summary>
        public static Restraint CompressionOnly(double? stiffness = null) =>
            new Restraint(stiffness is null, stiffness) { Sense = RestraintSense.CompressionOnly };

        /// <summary>
        /// A tie or an anchor: resists being pulled away, free in compression. SAF's
        /// <c>Tension only</c>, and <c>Flexible tension only</c> with a stiffness.
        /// </summary>
        public static Restraint TensionOnly(double? stiffness = null) =>
            new Restraint(stiffness is null, stiffness) { Sense = RestraintSense.TensionOnly };

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
