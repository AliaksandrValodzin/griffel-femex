namespace griffel_femex
{
    /// <summary>
    /// The 1.7 → 1.8 units migration: free text becomes an enum, or becomes nothing
    /// and says so.
    ///
    /// Lives beside the feature it concerns rather than beside the hook that runs
    /// it, the discipline <c>MigrateLegacyLoadIds</c> already follows from
    /// <c>FemexModel.Identity.cs</c>. The one migration that does <i>not</i> follow
    /// it is the 1.1 unit weight, which sits in <c>FemexModel.SelfWeight.cs</c>
    /// because converting γ into ρ needs the root's gravity; this one needs nothing
    /// but the string.
    ///
    /// <b>What does not parse is not carried.</b> <c>"length": "banana"</c> becomes
    /// no length unit at all, and <see cref="ReportMigrations"/> names the text it
    /// dropped. That text round-tripping clean through 1.7 is the exact defect the
    /// bump exists to fix — <c>FEMEX_SAF_Fit.md</c> §3 cites it by name — so losing
    /// it loudly is the change, not a regression in it. Keeping it would mean either
    /// a second free-text field beside the enum, which is the 1.7 design with an
    /// extra step, or a converter, which would be the first in this repository.
    /// </summary>
    public partial class FemexModel
    {
        // What the units migration did. Private fields, which System.Text.Json never
        // serializes, so no key can leak into a file. Like the unit-weight pair in
        // FemexModel.SelfWeight.cs they record a property of the read and not of the
        // model: a re-emitted file is 1.8 and carries typed units, so it must not
        // report again, and a model built in memory never migrates at all.
        private List<(string What, string Text, string Unit)>? _migratedUnits;
        private List<(string What, string Text)>? _droppedUnits;
        private List<string>? _bothUnitSpellings;

        /// <summary>
        /// Reads the 1.6/1.7 free-text <c>length</c> and <c>force</c> into
        /// <see cref="Units.Length"/> and <see cref="Units.Force"/>, and records both
        /// what it recognised and what it did not for <see cref="Validate()"/> to
        /// report.
        ///
        /// A block stating both spellings keeps the typed one: they cannot both be
        /// right and the newer one wins, which is the rule
        /// <c>MigrateLegacyUnitWeight</c> already applies to a material carrying both
        /// a <c>unitWeight</c> and a <c>density</c>. Reported all the same, because
        /// silently preferring one of two contradictory statements is the class of
        /// thing this repository never does quietly.
        /// </summary>
        private void MigrateLegacyUnits()
        {
            if (Units is null)
                return;

            if (Units.TryTakeLegacyLength(out string? length))
                Migrate("length", length, Units.Length is not null, ParseLengthUnit, u => Units.Length = u);

            if (Units.TryTakeLegacyForce(out string? force))
                Migrate("force", force, Units.Force is not null, ParseForceUnit, u => Units.Force = u);
        }

        /// <summary>
        /// One free-text unit: parsed and recorded, dropped and recorded, or left
        /// alone because the typed spelling is already there. Generic over the enum
        /// so that the two calls above cannot drift apart — the shape of this
        /// migration is identical for both and the only difference is the table.
        /// </summary>
        private void Migrate<T>(string what, string? text, bool alreadyTyped,
                                Func<string, T?> parse, Action<T> assign) where T : struct, Enum
        {
            // An empty or absent value is not a claim about anything, so there is
            // nothing to migrate and nothing to report. "  " is the same silence as
            // "" — a key written and left blank says no more than one never written.
            if (string.IsNullOrWhiteSpace(text))
                return;

            // text! for the netstandard annotation gap; see FormatNameKeyMessages
            // in FemexModel.Validation.cs.
            string trimmed = text!.Trim();

            if (alreadyTyped)
            {
                (_bothUnitSpellings ??= new List<string>()).Add(what);
                return;
            }

            if (parse(trimmed) is T unit)
            {
                assign(unit);
                (_migratedUnits ??= new List<(string, string, string)>())
                    .Add((what, trimmed, unit.ToString()));
            }
            else
            {
                (_droppedUnits ??= new List<(string, string)>())
                    .Add((what, trimmed));
            }
        }

        /// <summary>
        /// The length symbols a 1.6 or 1.7 file plausibly carries, case-insensitively
        /// — the symbol, the enum's own name, and the American spelling of the three
        /// metric ones. Null for anything else.
        ///
        /// A closed table and deliberately not a lenient one: every entry here is a
        /// spelling some program actually writes, and a parser that guessed would be
        /// inventing units, which is the failure <c>FEMEX_Adapters.md</c> §4.3 calls
        /// the one naive adapters never report.
        /// </summary>
        private static LengthUnit? ParseLengthUnit(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "mm": case "millimetre": case "millimeter": return LengthUnit.Millimetre;
                case "cm": case "centimetre": case "centimeter": return LengthUnit.Centimetre;
                case "m": case "metre": case "meter": return LengthUnit.Metre;
                case "in": case "inch": return LengthUnit.Inch;
                case "ft": case "foot": case "feet": return LengthUnit.Foot;
                default: return null;
            }
        }

        /// <summary>The force half of <see cref="ParseLengthUnit"/>.</summary>
        private static ForceUnit? ParseForceUnit(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "n": case "newton": return ForceUnit.Newton;
                case "kn": case "kilonewton": return ForceUnit.Kilonewton;
                case "mn": case "meganewton": return ForceUnit.Meganewton;
                case "lb": case "lbf": case "poundforce": case "pound-force": return ForceUnit.PoundForce;
                case "kip": case "kips": return ForceUnit.Kip;
                default: return null;
            }
        }
    }
}
