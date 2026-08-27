using System.ComponentModel;
using System.Text.Json.Serialization;

namespace griffel_femex.Loads
{
    /// <summary>
    /// A thermal load applied to one or more elements: a uniform temperature change
    /// and, from 1.9, up to two <b>signed</b> gradients across the element, one per
    /// local axis.
    ///
    /// <b>What 1.9 changed, and why it is a reinterpretation rather than a rename.</b>
    /// 1.6's <c>gradientPerDepth</c> was a single number with <i>no sign convention
    /// stated anywhere</i> — <c>FEMEX_SAF_Fit.md</c> §4 item 6 records that as one of
    /// the eight concepts crossing FEMEX silently wrong, because which face is the hot
    /// one decides which way the element curves. A number whose meaning was never
    /// fixed cannot be renamed into one whose meaning is; reading it as
    /// <see cref="GradientZ"/> gives it a sign convention it never had, and
    /// <c>FemexModel.Validate()</c> says so in those words on every file that carries
    /// the old key.
    ///
    /// <b>The convention, stated once.</b> A gradient is a temperature difference per
    /// unit length along the named local axis, and <b>positive means the temperature
    /// increases along the + direction of that axis</b>. The axes are the element's
    /// own — <c>FemexModel.TryGetBarLocalAxes</c> for a bar,
    /// <c>FemexModel.TryGetPlateLocalAxes</c> for a plate or a mesh face — so a
    /// gradient follows a rolled beam and a tilted panel without the load having to
    /// restate anything.
    ///
    /// A plate has one through-thickness axis, its local z, so
    /// <see cref="GradientY"/> on a plate element has nowhere to act;
    /// <c>Validate()</c> warns rather than the type forbidding it, since one load may
    /// name bars and plates together.
    /// </summary>
    public class TemperatureLoad : Load
    {
        // Element ids this temperature load applies to (references Element.Id)
        public List<int> ElementIds { get; set; } = new List<int>();

        // Uniform temperature change (e.g., in degrees Celsius)
        public double DeltaT { get; set; }

        /// <summary>
        /// The gradient along the element's local <b>y</b>, per unit length, signed.
        /// Null means none. Meaningful on a bar — the across-the-width gradient of a
        /// beam heated on one flank — and meaningless on a surface, which has only
        /// one through-thickness direction.
        /// </summary>
        public double? GradientY { get; set; }

        /// <summary>
        /// The gradient along the element's local <b>z</b>, per unit length, signed.
        /// Null means none. This is the through-depth gradient of a beam and the
        /// through-thickness gradient of a slab or wall, and it is where 1.6's
        /// <c>gradientPerDepth</c> is read to — see the type-level remarks for why
        /// that is reported as a reinterpretation.
        /// </summary>
        public double? GradientZ { get; set; }

        /// <summary>
        /// The 1.6/1.8 spelling, bound on read so nothing is dropped in silence.
        /// Deliberately <b>getter-less</b>: System.Text.Json can never write it back,
        /// so a 1.9 file cannot contain it — the same contract
        /// <c>Material.UnitWeight</c> has held since 1.2 and <c>Units.LegacyLength</c>
        /// since 1.8, and the reason the migration runs exactly once.
        /// </summary>
        [JsonPropertyName("gradientPerDepth")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public double? GradientPerDepth
        {
            set
            {
                _legacyGradient = value;
                _hasLegacyGradient = true;
            }
        }

        private double? _legacyGradient;
        private bool _hasLegacyGradient;

        /// <summary>
        /// Hands the pending 1.6 gradient to the migration and forgets it, so the
        /// reinterpretation can only ever happen once. False when the file carried no
        /// <c>gradientPerDepth</c> key at all.
        /// </summary>
        internal bool TryTakeLegacyGradient(out double? gradient)
        {
            gradient = _legacyGradient;

            if (!_hasLegacyGradient)
                return false;

            _legacyGradient = null;
            _hasLegacyGradient = false;
            return true;
        }
    }
}
