using griffel_femex.Loads;

namespace griffel_femex
{
    /// <summary>
    /// The 1.8 → 1.9 thermal-gradient migration: an unsigned number acquires a sign
    /// convention, which is a <b>reinterpretation</b> and not a rename.
    ///
    /// Lives beside the feature it concerns rather than beside the hook that runs it,
    /// the discipline <c>MigrateLegacyLoadIds</c> and <c>MigrateLegacyUnits</c>
    /// already follow.
    ///
    /// <b>Why the wording matters.</b> 1.8's units change dropped free text that
    /// named no unit and said so; a string disappearing loudly is a visible event.
    /// This one is worse in kind: <c>gradientPerDepth</c> is a real number that keeps
    /// its value and changes its meaning. The 1.6 field had <i>no sign convention
    /// stated anywhere</i> — that absence is precisely what <c>FEMEX_SAF_Fit.md</c>
    /// §4 item 6 complains about — so reading it as <see cref="TemperatureLoad.GradientZ"/>
    /// gives it one it never had, and which face of the element is the hot one is
    /// what decides which way the element curves.
    ///
    /// <b>So the migration does not choose.</b> It carries the number across
    /// unaltered and reports, on every load it touched, that the reading is a
    /// reinterpretation and that the author should confirm the sign. Nothing here
    /// inspects the element, guesses at an intent or flips a value: an adapter or a
    /// person deciding deliberately is the only honest way to settle it, and this
    /// repository's own reference file was hand-migrated for exactly that reason
    /// (<c>Claude/FEMEX_LoadGroups_Summary.md</c> records the decision and its
    /// reasoning).
    /// </summary>
    public partial class FemexModel
    {
        // Which loads the gradient migration reinterpreted, and to what. A private
        // field, which System.Text.Json never serializes, recording a property of the
        // read rather than of the model: a re-emitted file is 1.9 and carries
        // gradientZ, so it must not report again, and a model built in memory never
        // migrates at all.
        private List<(int Id, string? Label, double Value)>? _reinterpretedGradients;

        // The loads that stated both spellings. Same contract as the field above.
        private List<(int Id, string? Label)>? _bothGradientSpellings;

        /// <summary>
        /// Reads each 1.6/1.8 <c>gradientPerDepth</c> into
        /// <see cref="TemperatureLoad.GradientZ"/> and records what it did for
        /// <see cref="Validate()"/> to report.
        ///
        /// A load stating both spellings keeps the typed one: they cannot both be
        /// right and the newer one wins, which is the rule
        /// <c>MigrateLegacyUnitWeight</c> applies to a material carrying both a
        /// <c>unitWeight</c> and a <c>density</c>, and <c>MigrateLegacyUnits</c> to a
        /// units block carrying both spellings. It is reported all the same.
        ///
        /// An explicit <c>null</c> is a key that says "no gradient", which
        /// <see cref="TemperatureLoad.GradientZ"/> already says by being absent; it is
        /// taken and dropped without a message, because nothing was reinterpreted.
        /// </summary>
        private void MigrateLegacyGradients()
        {
            foreach (var load in Loads)
            {
                if (load is not TemperatureLoad temperature)
                    continue;

                if (!temperature.TryTakeLegacyGradient(out double? gradient))
                    continue;

                if (gradient is not double value)
                    continue;

                if (temperature.GradientZ is not null)
                {
                    (_bothGradientSpellings ??= new List<(int, string?)>())
                        .Add((temperature.Id, temperature.Label));
                    continue;
                }

                temperature.GradientZ = value;

                (_reinterpretedGradients ??= new List<(int, string?, double)>())
                    .Add((temperature.Id, temperature.Label, value));
            }
        }
    }
}
