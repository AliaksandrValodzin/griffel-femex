namespace griffel_femex
{
    /// <summary>
    /// The unit every angle in the model is in — <c>Bar.RotationAngle</c>,
    /// <c>Plate.LocalAxisAngle</c>, <c>Grid.RotationAngle</c>.
    ///
    /// Closed for the reason <see cref="LengthUnit"/> gives, and with two members:
    /// gradians exist and no structural program offers them.
    ///
    /// <b>FEMEX's own angles are degrees</b>, stated as such on each of the three
    /// properties above, and nothing in this library reads this enum. It is here
    /// because a check report has to print an angle with a unit beside it and
    /// because SAF states its rotations in degrees explicitly rather than by
    /// convention — an annotation that agrees with the convention, not a switch that
    /// changes it. A file stating <see cref="Radian"/> is stating something FEMEX's
    /// own properties contradict, which is a thing a reader should be able to see.
    /// </summary>
    public enum AngleUnit
    {
        /// <summary>Degrees — what every angle in FEMEX is in.</summary>
        Degree,

        /// <summary>Radians.</summary>
        Radian,
    }
}
