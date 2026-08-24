namespace griffel_femex
{
    /// <summary>
    /// The unit every length in the model is in — coordinates, elevations, section
    /// dimensions, plate thicknesses, and the length half of every derived unit.
    ///
    /// An enum where 1.6's <see cref="Units.Length"/> was free text, and the line is
    /// the one <see cref="Geometry.Sections.SectionManufacture"/> draws against
    /// <c>SectionCatalogue.Source</c>, read the other way round: the set of national
    /// section libraries is open and still growing, whereas the set of units an
    /// analysis model measures length in is small and has been closed since the
    /// metre and the foot. Free text bought nothing and cost the one thing that
    /// mattered — <c>"length": "banana"</c> round-tripped clean through 1.7, so the
    /// annotation could not be relied on by the very report it exists for.
    ///
    /// Five members and no more. <c>Kilometre</c>, <c>Micrometre</c>, <c>Yard</c>
    /// and <c>Mile</c> are deliberately absent: nothing is modelled at those scales,
    /// and an enum that admits a unit no structural model uses invites an exporter
    /// to write one. A file needing one carries it as an unknown member on the units
    /// block, where <see cref="FemexModel.Validate()"/> names it.
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>Millimetres — the usual unit of a section dimension.</summary>
        Millimetre,

        /// <summary>Centimetres, as central-European section tables state them.</summary>
        Centimetre,

        /// <summary>Metres — the usual unit of a coordinate.</summary>
        Metre,

        /// <summary>Inches.</summary>
        Inch,

        /// <summary>Feet.</summary>
        Foot,
    }
}
