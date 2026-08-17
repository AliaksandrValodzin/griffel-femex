namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// A section with no geometry at all — SAF's <c>General</c>. It carries only
    /// <see cref="Section.Properties"/>, and it is what an adapter reaches for when
    /// the native model had a shape FEMEX does not model: the member crosses by its
    /// stiffness instead of being dropped.
    ///
    /// <see cref="CalculateArea"/> returns zero because there is nothing to compute
    /// from, and it is <b>not</b> the accessor to use — <see cref="Section.GetArea"/>
    /// is, and it returns the stated area. A generic section that states no area is
    /// an error: it has neither geometry nor stiffness, so nothing can be built
    /// from it.
    /// </summary>
    public class GenericSection : Section
    {
        // Parameterless constructor for serialization
        public GenericSection() { }

        // Convenience constructor
        public GenericSection(int id, string? name, SectionProperties? properties = null)
        {
            Id = id;
            Name = name;
            Properties = properties;
        }

        /// <summary>
        /// Zero: this section has no geometry to compute an area from. See
        /// <see cref="Section.GetArea"/>, which is the only meaningful accessor here.
        /// </summary>
        public override double CalculateArea()
        {
            return 0.0;
        }
    }
}
