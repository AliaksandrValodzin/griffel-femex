using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry
{
    /// <summary>
    /// How far the bar sits off the line its two nodes define, at each end, in each
    /// of the bar's own local directions — and, crucially, <b>in each of the two
    /// senses that word has</b>.
    ///
    /// <b>The split is SAF's and it is the point of this type.</b>
    /// <c>FEMEX_SAF_Fit.md</c> calls it "the most honest model of the three and worth
    /// copying", and the reason is that the two families do different things:
    ///
    /// <list type="bullet">
    /// <item><b>Structural</b> is the BIM offset — where the physical member is
    /// drawn relative to its setting-out line. It moves the picture and the clash
    /// model. It does <i>not</i> change internal forces.</item>
    /// <item><b>Analysis</b> moves the analysis line itself, so the member's axial
    /// force acts on a lever arm it did not have before. It <i>does</i> change
    /// internal forces, and it is the one that turns a beam framing into a column
    /// face into a beam that also applies a moment there.</item>
    /// </list>
    ///
    /// A receiver that collapses the two produces geometry that looks right and
    /// stiffness that is wrong — which is why the eight fields are named apart rather
    /// than fused into four. Most programs fuse them; FEMEX does not, and an adapter
    /// crossing into one that does reports the loss.
    ///
    /// All eight are in the bar's own local y and z, the frame
    /// <c>FemexModel.TryGetBarLocalAxes</c> produces and <c>Bar.RotationAngle</c>
    /// rolls, measured from the system line
    /// <see cref="Bar.Alignment"/> names — so an eccentricity is a correction on top
    /// of an alignment and never a substitute for one.
    ///
    /// Every field is nullable with no initializer, so a block that states two
    /// offsets writes two keys. A block whose eight fields are all null is an empty
    /// claim and <c>FemexModel.Validate()</c> says so, the same treatment
    /// <c>ValidateSectionCompleteness</c> gives a section that states nothing.
    ///
    /// <b>Nothing in the corpus exercises this.</b> All four analysis columns are zero
    /// on all forty-two members of every published SAF file
    /// (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.4). That is an argument for
    /// shipping the shape and for not spending long on it, not an argument against
    /// carrying it: the columns are mandatory, so an exporter has to write something,
    /// and a format with nowhere to read one has to invent all four.
    /// </summary>
    public class BarEccentricity : IExtensible
    {
        /// <summary>Structural offset along local y at the start node.</summary>
        public double? StructuralYBegin { get; set; }

        /// <summary>Structural offset along local z at the start node.</summary>
        public double? StructuralZBegin { get; set; }

        /// <summary>Structural offset along local y at the end node.</summary>
        public double? StructuralYEnd { get; set; }

        /// <summary>Structural offset along local z at the end node.</summary>
        public double? StructuralZEnd { get; set; }

        /// <summary>Analysis offset along local y at the start node. Changes forces.</summary>
        public double? AnalysisYBegin { get; set; }

        /// <summary>Analysis offset along local z at the start node. Changes forces.</summary>
        public double? AnalysisZBegin { get; set; }

        /// <summary>Analysis offset along local y at the end node. Changes forces.</summary>
        public double? AnalysisYEnd { get; set; }

        /// <summary>Analysis offset along local z at the end node. Changes forces.</summary>
        public double? AnalysisZEnd { get; set; }

        /// <summary>
        /// Whether the block states anything at all. A method rather than a
        /// property, the convention <c>Section.CalculateArea</c> and
        /// <c>Material.GetShearModulus</c> already follow: a computed value is not
        /// data and must not be serialized, and there is no <c>[JsonIgnore]</c> in
        /// this repository to make one that is not. Read by
        /// <c>FemexModel.Validate()</c>, which reports one that does not.
        /// </summary>
        public bool IsEmpty() =>
            StructuralYBegin is null && StructuralZBegin is null &&
            StructuralYEnd is null && StructuralZEnd is null &&
            AnalysisYBegin is null && AnalysisZBegin is null &&
            AnalysisYEnd is null && AnalysisZEnd is null;

        /// <summary>
        /// Whether any of the four analysis offsets is a non-zero number. This is
        /// the half that changes the answer, so it is the half a report names.
        /// </summary>
        public bool MovesTheAnalysisLine() =>
            AnalysisYBegin is not (null or 0.0) || AnalysisZBegin is not (null or 0.0) ||
            AnalysisYEnd is not (null or 0.0) || AnalysisZEnd is not (null or 0.0);

        // Members this build does not know; see IExtensible. Its own property: the
        // attribute is not inherited through the interface.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
