using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex
{
    /// <summary>
    /// What the numbers in this model are in. Pure annotation: nothing in this
    /// library computes with it, and no number anywhere in FEMEX is converted by it.
    /// Every value in the file is in the model's own units and always was; this
    /// block is what lets a reader — human or program — find out which ones those
    /// are.
    ///
    /// <b>Typed from 1.8, free text before it.</b> <see cref="Length"/> and
    /// <see cref="Force"/> were <c>string?</c> with comment-level guidance and no
    /// validation, so <c>"length": "banana"</c> round-tripped clean — an annotation
    /// nothing could rely on, which is the only defect an annotation can have. The
    /// 1.6/1.7 spellings are still read, migrated once, and reported by
    /// <see cref="FemexModel.Validate()"/>; text that parses to no unit is
    /// <b>dropped and named</b>, because carrying it forward is what the change
    /// exists to stop.
    ///
    /// Five independent quantities rather than one system flag, and that choice has
    /// a cost worth stating: <b>this block does not supply SAF's mandatory
    /// <c>Model.System of units</c></b>, which is a single <c>Metric | Imperial</c>
    /// value about the whole model. Five enums can express <c>Metre</c> with
    /// <c>Kip</c>, which is neither, so an adapter reports that column as
    /// <i>Invented</i> per <c>FEMEX_Adapters.md</c> §4.3 — as it does
    /// <c>Model.National code</c>, and as it does <c>Model.LCS of cross-section</c>,
    /// which FEMEX fixes by convention and states nowhere. Independence is still the
    /// right shape: real models are mixed, section tables are in millimetres while
    /// coordinates are in metres, and a flag that forbade saying so would be a
    /// worse annotation than none.
    ///
    /// Nullable with no initializer at the root, like <see cref="FemexModel.Metadata"/>
    /// and unlike <see cref="Gravity"/>: gravity is consumed, this is not, and a
    /// model that says nothing about its units omits the key rather than writing an
    /// empty block.
    /// </summary>
    public class Units : IExtensible
    {
        /// <summary>
        /// The unit of every length: coordinates, elevations, section dimensions,
        /// thicknesses. Null means the model does not say.
        ///
        /// Its JSON key is <c>lengthUnit</c>, not <c>length</c>, and that is the one
        /// non-additive change in 1.8. <c>"length": "m"</c> and
        /// <c>"length": "Metre"</c> cannot share a key without a custom converter,
        /// and there is not one converter in this repository — the global
        /// <c>JsonStringEnumConverter</c> and camelCase policy cover everything, and
        /// the first exception would be the expensive one. A new key instead, with
        /// the old one bound to <see cref="LegacyLength"/> below.
        /// </summary>
        [JsonPropertyName("lengthUnit")]
        public LengthUnit? Length { get; set; }

        /// <summary>
        /// The unit of every force. Null means the model does not say. Keyed
        /// <c>forceUnit</c> for the reason <see cref="Length"/> gives.
        /// </summary>
        [JsonPropertyName("forceUnit")]
        public ForceUnit? Force { get; set; }

        /// <summary>
        /// The unit a temperature load's ΔT is in. New in 1.8 with no free-text
        /// predecessor, so it needs no key of its own and takes the camelCase one.
        /// </summary>
        public TemperatureUnit? Temperature { get; set; }

        /// <summary>The unit every angle is in. See <see cref="AngleUnit"/>.</summary>
        public AngleUnit? Angle { get; set; }

        /// <summary>The unit <c>Material.Density</c> is in. See <see cref="MassUnit"/>.</summary>
        public MassUnit? Mass { get; set; }

        /// <summary>
        /// The 1.6/1.7 spelling, bound on read so nothing is dropped in silence.
        /// Deliberately <b>getter-less</b>: System.Text.Json can never write it back,
        /// so a 1.8 file cannot contain it — the same contract
        /// <c>Material.UnitWeight</c> has held since 1.2, and the reason a migration
        /// runs exactly once.
        /// </summary>
        [JsonPropertyName("length")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? LegacyLength
        {
            set
            {
                _legacyLength = value;
                _hasLegacyLength = true;
            }
        }

        private string? _legacyLength;
        private bool _hasLegacyLength;

        /// <summary>The 1.6/1.7 force spelling; see <see cref="LegacyLength"/>.</summary>
        [JsonPropertyName("force")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string? LegacyForce
        {
            set
            {
                _legacyForce = value;
                _hasLegacyForce = true;
            }
        }

        private string? _legacyForce;
        private bool _hasLegacyForce;

        public Units() { }

        /// <summary>
        /// The two units a structural model almost always states. Typed from 1.8,
        /// where 1.7's <c>Units(string?, string?)</c> took the symbols directly.
        ///
        /// The old signature is <b>gone rather than overloaded</b>, so a call
        /// written against 1.7 fails to compile. That is the point: 1.7's
        /// <c>Material(…, density, …)</c> kept its shape while its meaning changed
        /// by a factor of g, and its own doc comment records that a positional call
        /// written against 1.1 "still compiles and now means something a thousandth
        /// of the size the author intended". A loud break is the better of the two,
        /// and this is the bump where FEMEX gets to choose it.
        /// </summary>
        public Units(LengthUnit? length, ForceUnit? force)
        {
            Length = length;
            Force = force;
        }

        /// <summary>
        /// Hands the pending free-text length to the migration and forgets it, so
        /// the parse can only ever happen once. False when the file carried no
        /// <c>length</c> key at all — which is a different answer from carrying an
        /// empty one, and the migration reports the two differently.
        /// </summary>
        internal bool TryTakeLegacyLength(out string? length)
        {
            length = _legacyLength;

            if (!_hasLegacyLength)
                return false;

            _legacyLength = null;
            _hasLegacyLength = false;
            return true;
        }

        /// <summary>The force half of <see cref="TryTakeLegacyLength"/>.</summary>
        internal bool TryTakeLegacyForce(out string? force)
        {
            force = _legacyForce;

            if (!_hasLegacyForce)
                return false;

            _legacyForce = null;
            _hasLegacyForce = false;
            return true;
        }

        // Members this build does not know; see IExtensible. The 1.6 spellings
        // length and force are not among them — they are declared properties above,
        // so the migration is untouched by extension data.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
