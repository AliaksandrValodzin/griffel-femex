namespace griffel_femex.Geometry
{
    /// <summary>
    /// Which line of the cross-section the bar's two nodes lie on — SAF's
    /// <c>StructuralCurveMember.System line</c>, whose nine values these are exactly.
    /// The bar counterpart of <see cref="SurfaceAlignment"/>, which says the same
    /// thing about a panel in three values rather than nine because a surface has one
    /// thickness direction and a section has two.
    ///
    /// <b>Mandatory in SAF and not always <see cref="Centre"/>.</b> The reference
    /// workbook's forty-two members are thirty-three <c>Top left</c> and nine
    /// <c>Centre</c> (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.4), so an exporter
    /// that assumed the centre would move two thirds of that file's members by half a
    /// section depth.
    ///
    /// The names are in the bar's own local y and z, the frame
    /// <c>FemexModel.TryGetBarLocalAxes</c> produces and <c>Bar.RotationAngle</c>
    /// rolls: <b>top</b> is the +z face, <b>left</b> the +y face. Nothing new is
    /// invented — it is the frame the section's own dimensions are already stated in.
    ///
    /// <b>Null is <see cref="Centre"/></b>, for the reason
    /// <see cref="BarBehaviour"/> gives: silence and a stated centre are different
    /// facts, and only silence keeps an existing file byte-identical.
    ///
    /// This is the <i>system line</i>, not an offset. An arbitrary shift of the
    /// analysis line away from it is <see cref="BarEccentricity"/>, and the two are
    /// separate because they answer to different owners — an alignment is how the
    /// member was set out, an eccentricity is a correction applied to it.
    /// </summary>
    public enum BarAlignment
    {
        /// <summary>The section's centroid. What every bar written before 1.10 means.</summary>
        Centre,

        /// <summary>The mid-point of the +z face.</summary>
        Top,

        /// <summary>The mid-point of the -z face.</summary>
        Bottom,

        /// <summary>The mid-point of the +y face.</summary>
        Left,

        /// <summary>The mid-point of the -y face.</summary>
        Right,

        /// <summary>The +y, +z corner.</summary>
        TopLeft,

        /// <summary>The -y, +z corner.</summary>
        TopRight,

        /// <summary>The +y, -z corner.</summary>
        BottomLeft,

        /// <summary>The -y, -z corner.</summary>
        BottomRight,
    }
}
